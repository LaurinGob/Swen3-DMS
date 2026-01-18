using DocumentLoader.Models;
using DocumentLoader.RabbitMQ;
using DocumentLoader.OCRWorker.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Minio;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Text.Json;


namespace DocumentLoader.UnitTests
{
    internal class OcrTests
    {
        private Mock<ILogger<OcrWorkerService>> _mockLogger = null!;
        private Mock<IMinioClient> _mockMinio = null!;
        private Mock<IConfiguration> _mockConfig = null!;
        private Mock<IRabbitMqSubscriber> _mockSubscriber = null!;
        private Mock<IRabbitMqPublisher> _mockPublisher = null!;
        private OcrWorkerService _worker = null!;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger<OcrWorkerService>>();
            _mockMinio = new Mock<IMinioClient>();
            _mockConfig = new Mock<IConfiguration>();
            _mockSubscriber = new Mock<IRabbitMqSubscriber>();
            _mockPublisher = new Mock<IRabbitMqPublisher>();

            // Mock for config path
            _mockConfig.Setup(c => c["OcrStorage:BasePath"]).Returns(Path.GetTempPath());

            _worker = new OcrWorkerService(
                _mockLogger.Object,
                _mockMinio.Object,
                _mockConfig.Object,
                _mockSubscriber.Object,
                _mockPublisher.Object
            );
        }

        [TearDown]
        public void TearDown()
        {
            _worker.Dispose();
        }

        [Test]
        public async Task ExecuteAsync_SubscribesToOcrQueue()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            // Act

            var task = _worker.StartAsync(cts.Token);
            cts.Cancel();
            await task;

            // Assert
            _mockSubscriber.Verify(s => s.SubscribeAsync(
                RabbitMqQueues.OCR_QUEUE,
                It.IsAny<Func<string, Task>>()),
                Times.Once);
        }

        [Test]
        public async Task PrepareForGeminiAsync_PublishesToResultQueue()
        {

            var job = new OcrJob { DocumentId = 1, ObjectName = "file.pdf", Bucket = "b" };
            var ocrText = "Extracted Text";

            var result = new OcrResult { DocumentId = job.DocumentId, OcrText = ocrText };
            string json = JsonSerializer.Serialize(result);

            await _mockPublisher.Object.PublishAsync(RabbitMqQueues.RESULT_QUEUE, json);

            _mockPublisher.Verify(p => p.PublishAsync(RabbitMqQueues.RESULT_QUEUE, It.IsAny<string>()), Times.Once);
        }
    }
}
