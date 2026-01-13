using DocumentLoader.RabbitMQ;
using DocumentLoader.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _configuration;


        public OcrWorkerService(ILogger<OcrWorkerService> logger, IMinioClient minio, IConfiguration configuration)
        {
            _logger = logger;
            _minio = minio;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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

                        _logger.LogInformation($"[OCRWorker] Processing object:  documentId={job.DocumentId}, bucket={job.Bucket}, object={job.ObjectName}");

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

            // Keep the worker process alive
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // normal on shutdown
            }
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
                Arguments = $"{localPdf} {outputPrefix} -r 300 -png",
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var proc = Process.Start(psi);
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
                throw new Exception($"pdftoppm failed: {stderr}");

            var prefixName = Path.GetFileName(outputPrefix);
            var images = Directory.GetFiles("/tmp", $"{prefixName}-*.*")
                .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg"))
                .OrderBy(f => f)
                .ToArray();

            if (images.Length == 0)
                throw new Exception("Poppler produced no images.");

            var sb = new StringBuilder();
            var tessdata = Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
              ?? "/usr/share/tesseract-ocr/5/tessdata/";
           
            using var engine = new TesseractEngine(tessdata, "deu+eng", EngineMode.LstmOnly);

            engine.DefaultPageSegMode = PageSegMode.Auto;

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

            _logger.LogInformation("OCR text processed. Content: " + sb.ToString().Substring(0, Math.Min(200, sb.Length)) + "...");
            return sb.ToString();
        }



        private Task PrepareForGeminiAsync(OcrJob job, string ocrText)
        {

            SaveOcrToFile(job, ocrText);


            var result = new OcrResult
            {
                DocumentId = job.DocumentId,
                Bucket = job.Bucket,
                ObjectName = job.ObjectName,
                OcrText = ocrText
            };

            string json = JsonSerializer.Serialize(result);

            // Publish to the queue your GenAI worker listens to
            RabbitMqPublisher.Instance.Publish(RabbitMqQueues.RESULT_QUEUE, json);

            _logger.LogInformation($"[OCRWorker] Published OCR result for {job.ObjectName} to RESULT_QUEUE. Length={ocrText.Length}");

            return Task.CompletedTask;
        }

        private void SaveOcrToFile(OcrJob job, string ocrText)
        {
            var basePath =
                _configuration["OcrStorage:BasePath"]
                ?? Path.Combine(AppContext.BaseDirectory, "OcrResults");

            // Folder per document
            var documentFolder = Path.Combine(basePath, job.DocumentId.ToString());

            Directory.CreateDirectory(documentFolder);

            // Safe filename
            var safeFileName = Path.GetFileName(job.ObjectName) + ".txt";
            var filePath = Path.Combine(documentFolder, safeFileName);

            var content = $"""
            # DocumentId: {job.DocumentId}
            # Bucket: {job.Bucket}
            # ObjectName: {job.ObjectName}
            # GeneratedAt (UTC): {DateTime.UtcNow:O}

            {ocrText}
            """;

            File.WriteAllText(filePath, content, Encoding.UTF8);

            _logger.LogInformation(
                "[OCRWorker] OCR text written to {Path}",
                filePath
            );
        }


    }
}
