using DocumentLoader.RabbitMQ;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace DocumentLoader.OCRWorker.Services
{
    public class OcrWorkerService : BackgroundService
    {
        private readonly ILogger<OcrWorkerService> _logger;

        public OcrWorkerService(ILogger<OcrWorkerService> logger)
        {
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Subscribe to OCR_Queue
            RabbitMqSubscriber.Instance.Subscribe(RabbitMqQueues.OCR_QUEUE, async documentPath =>
            {
                _logger.LogInformation($"[OCRWorker] Received document: {documentPath}");

                PerformOcr(documentPath);

                var summary = $"Processed OCR result for {documentPath}";

                // Publish result back to Result_Queue
                RabbitMqPublisher.Instance.Publish(RabbitMqQueues.RESULT_QUEUE, summary);

                _logger.LogInformation($"[OCRWorker] Sent result: {summary}");
            });

            return Task.CompletedTask;
        }

        private string PerformOcr(string imagePath)
        {
            string resultText = "";
            using var engine = new TesseractEngine("/usr/share/tessdata", "eng", EngineMode.Default);
            using var img = Pix.LoadFromFile(imagePath);
            using var page = engine.Process(img);
            resultText = page.GetText();
            return resultText;
        }
    }
}
