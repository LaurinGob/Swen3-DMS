using DocumentLoader.BatchProcessing;
using DocumentLoader.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace DocumentLoader.UnitTests
{
    public class BatchProcessingTests
    {
        private Mock<IAccessLogSink> _mockSink = null!;
        private Mock<ILogger> _mockLogger = null!;
        private AccessLogBatchProcessor _processor = null!;

        private string _basePath = null!;
        private string _inputPath = null!;
        private string _archivePath = null!;
        private string _errorPath = null!;

        [SetUp]

        public void Setup()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "BatchTests");
            _inputPath = Path.Combine(_basePath, "Input");
            _archivePath = Path.Combine(_basePath, "Archive");
            _errorPath = Path.Combine(_basePath, "Error");

            if (Directory.Exists(_basePath)) Directory.Delete(_basePath, true);

            _mockSink = new Mock<IAccessLogSink>();
            _mockLogger = new Mock<ILogger>();

            _processor = new AccessLogBatchProcessor(
                _inputPath, _archivePath, _errorPath, "*.xml",
                _mockSink.Object, _mockLogger.Object);
        }

        // AccessBatchJob tests

        [Test]
        public async Task RunOnce_NoFiles_LogsInformation()
        {
            // Act
            await _processor.RunOnceAsync();

            // Assert
            _mockSink.Verify(s => s.StoreBatchAsync(It.IsAny<List<DailyAccessDto>>()), Times.Never);
        }

        [Test]
        public async Task RunOnce_ValidXml_ProcessesAndMovesToArchive()
        {
            // Arrange
            Directory.CreateDirectory(_inputPath);
            var xmlContent = new XDocument(
                new XElement("accessLog", new XAttribute("batchDate", "2024-01-01"),
                    new XElement("entry", new XAttribute("documentId", "1"), new XAttribute("accessCount", "10"))
                )
            );
            string filePath = Path.Combine(_inputPath, "test.xml");
            xmlContent.Save(filePath);

            // Act
            await _processor.RunOnceAsync();

            // Assert
            _mockSink.Verify(s => s.StoreBatchAsync(It.Is<List<DailyAccessDto>>(l => l.Count == 1)), Times.Once);
            Assert.IsFalse(File.Exists(filePath), "File should be moved from input");
            Assert.IsTrue(File.Exists(Path.Combine(_archivePath, "test.xml")), "File should be in archive");
        }

        [Test]
        public async Task RunOnce_InvalidXml_MovesToErrorFolder()
        {
            // Arrange
            Directory.CreateDirectory(_inputPath);
            string filePath = Path.Combine(_inputPath, "corrupt.xml");
            await File.WriteAllTextAsync(filePath, "NOT XML CONTENT");

            // Act
            await _processor.RunOnceAsync();

            // Assert
            _mockSink.Verify(s => s.StoreBatchAsync(It.IsAny<List<DailyAccessDto>>()), Times.Never);
            Assert.IsTrue(File.Exists(Path.Combine(_errorPath, "corrupt.xml")), "Corrupt file should be in error folder");
        }

        //AccessLogApiSink tests

        [Test]
        public async Task StoreBatchAsync_SuccessfulResponse_ReturnsNormally()
        {
            // Arrange
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage
               {
                   StatusCode = HttpStatusCode.OK
               });

            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
            var sink = new AccessLogApiSink(httpClient);
            var data = new List<DailyAccessDto> { new DailyAccessDto { DocumentId = 1 } };

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await sink.StoreBatchAsync(data));
        }

        [Test]
        public async Task StoreBatchAsync_ApiReturnsError_ThrowsException()
        {
            // Arrange
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage
               {
                   StatusCode = HttpStatusCode.InternalServerError
               });

            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
            var sink = new AccessLogApiSink(httpClient);

            // Act & Assert
            Assert.ThrowsAsync<HttpRequestException>(async () => await sink.StoreBatchAsync(new List<DailyAccessDto>()));
        }

    }
}
