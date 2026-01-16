using DocumentLoader.Models;
using Microsoft.Extensions.Logging;
using Quartz.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace DocumentLoader.BatchProcessing
{
    public class AccessLogBatchProcessor
    {
        private readonly string _inputFolder;
        private readonly string _archiveFolder;
        private readonly string _errorFolder;
        private readonly string _filePattern;
        private readonly IAccessLogSink _sink;
        private readonly ILogger _logger;

        private record AccessEntry(int DocumentId, int AccessCount);

        public AccessLogBatchProcessor(
            string inputFolder,
            string archiveFolder,
            string errorFolder,
            string filePattern,
            IAccessLogSink sink,
            ILogger logger)
        {
            _inputFolder = inputFolder;
            _archiveFolder = archiveFolder;
            _errorFolder = errorFolder;
            _filePattern = filePattern;
            _sink = sink;
            _logger = logger;
        }

        public async Task RunOnceAsync()
        {
            Directory.CreateDirectory(_inputFolder);
            Directory.CreateDirectory(_archiveFolder);
            Directory.CreateDirectory(_errorFolder);
            _logger.LogInformation("Starting batch processing...");

            var files = Directory.GetFiles(_inputFolder, _filePattern)
                .OrderBy(f => f)
                .ToList();

            if (files.Count == 0)
            {
                _logger.LogInformation("No files to process.");
                return;
            }

            foreach (var file in files)
            {
                _logger.LogInformation($"Processing file: {Path.GetFileName(file)}");
                await ProcessSingleFileAsync(file);
            }
        }

        private async Task ProcessSingleFileAsync(string path)
        {
            var fileName = Path.GetFileName(path);

            try
            {
                var (batchDate, entries) = ReadXml(path);
                _logger.LogInformation($"Read {entries.Count} entries for batch date {batchDate}.");

                // 1. Convert our internal record list to the DTO list the API expects
                var dtos = entries.Select(e => new DailyAccessDto
                {
                    DocumentId = e.DocumentId,
                    AccessCount = e.AccessCount,
                    Date = batchDate
                }).ToList();

                // 2. Call the NEW bulk sink method (No more loop here!)
                await _sink.StoreBatchAsync(dtos);

                _logger.LogInformation($"Successfully processed and synced batch for {fileName}");
                MoveSafe(path, Path.Combine(_archiveFolder, fileName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing file {fileName}: {ex.Message}");
                MoveSafe(path, Path.Combine(_errorFolder, fileName));
            }
        }

        private static (DateOnly batchDate, List<AccessEntry> entries) ReadXml(string path)
        {
            var xdoc = XDocument.Load(path);
            var root = xdoc.Root ?? throw new InvalidDataException("Missing root element.");
           

            var batchDateAttr = root.Attribute("batchDate")?.Value
                ?? throw new InvalidDataException("Missing 'batchDate' attribute.");

            var batchDate = DateOnly.Parse(batchDateAttr);

            var entries = root.Elements("entry")
                .Select(e =>
                {
                    var docIdStr = e.Attribute("documentId")?.Value
                        ?? throw new InvalidDataException("Missing 'documentId'.");

                    var countStr = e.Attribute("accessCount")?.Value
                        ?? throw new InvalidDataException("Missing 'accessCount'.");

                    // Parse as int instead of Guid
                    if (!int.TryParse(docIdStr, out var docId))
                        throw new InvalidDataException($"Invalid documentId: {docIdStr}");

                    if (!int.TryParse(countStr, out var count) || count < 0)
                        throw new InvalidDataException($"Invalid accessCount: {countStr}");

                    return new AccessEntry(docId, count);
                })
                .ToList();

            return (batchDate, entries);
        }

        private static void MoveSafe(string src, string dst)
        {
            var dir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(dst))
            {
                File.Delete(dst);
            }

            File.Move(src, dst);
        }
    }
}
