using DocumentLoader.RabbitMQ;
using DocumentLoader.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Tesseract;

namespace DocumentLoader.OCRWorker.Services
{
    public class OcrWorkerService : BackgroundService
    {
        private readonly ILogger<OcrWorkerService> _logger;
        private readonly IMinioClient _minio;

        public OcrWorkerService(ILogger<OcrWorkerService> logger, IMinioClient minio)
        {
            _logger = logger;
            _minio = minio;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            RabbitMqSubscriber.Instance.Subscribe(
                RabbitMqQueues.OCR_QUEUE,
                async messageJson =>
                {
                    try
                    {
                        var job = JsonSerializer.Deserialize<OcrJob>(messageJson);
                        if (job == null)
                        {
                            _logger.LogWarning("[OCRWorker] Received invalid OCR job message.");
                            return;
                        }

                        _logger.LogInformation($"[OCRWorker] Processing object: bucket={job.Bucket}, object={job.ObjectName}");

                        // Perform OCR
                        string ocrText = await ProcessDocumentAsync(job);

                        // Prepare for Gemini API
                        await PrepareForGeminiAsync(job, ocrText);

                        _logger.LogInformation($"[OCRWorker] OCR completed ({ocrText.Length} chars) for {job.ObjectName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[OCRWorker] OCR processing failed.");
                    }
                });

            return Task.CompletedTask;
        }

        private async Task<string> ProcessDocumentAsync(OcrJob job)
        {
            // Create unique temp file names
            string localPdf = $"/tmp/{Guid.NewGuid()}.pdf";
            string outputPrefix = $"/tmp/page-{Guid.NewGuid()}";

            _logger.LogInformation("[OCRWorker] Downloading PDF from MinIO...");
            await _minio.GetObjectAsync(new GetObjectArgs()
                .WithBucket(job.Bucket)
                .WithObject(job.ObjectName)
                .WithFile(localPdf));

            if (!File.Exists(localPdf))
                throw new Exception("Failed to download PDF from MinIO!");

            _logger.LogInformation("[OCRWorker] Converting PDF to images with pdftoppm...");
            var psi = new ProcessStartInfo
            {
                FileName = "pdftoppm",
                Arguments = $"{localPdf} {outputPrefix} -jpeg",
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var proc = Process.Start(psi);
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
                throw new Exception($"pdftoppm failed: {stderr}");

            var images = Directory.GetFiles("/tmp", "page-*.jpg");
            if (images.Length == 0)
                throw new Exception("Poppler produced no images.");

            var sb = new StringBuilder();
            using var engine = new TesseractEngine("/usr/share/tessdata", "eng", EngineMode.Default);

            foreach (var imgPath in images.OrderBy(p => p))
            {
                _logger.LogInformation("[OCRWorker] Reading image: " + imgPath);
                using var img = Pix.LoadFromFile(imgPath);
                using var page = engine.Process(img);
                sb.AppendLine(page.GetText());
            }

            // Cleanup temp files
            try
            {
                File.Delete(localPdf);
                foreach (var img in images) File.Delete(img);
            }
            catch { /* ignore cleanup failures */ }

            return sb.ToString();
        }

        private Task PrepareForGeminiAsync(OcrJob job, string ocrText)
        {
            // TODO: implement your integration
            // e.g., save to database, queue, or call Gemini API
            _logger.LogInformation($"[OCRWorker] Ready to send OCR text for Gemini API. Length={ocrText.Length}");
            return Task.CompletedTask;
        }
    }
}
