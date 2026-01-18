using DocumentLoader.API.Controllers;
using DocumentLoader.Core.Services;
using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using DocumentLoader.RabbitMQ;
using Elastic.Clients.Elasticsearch;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Moq;
using NUnit.Framework;
using System.Text;
using System.Text.Json;

namespace DocumentLoader.UnitTests
{
    [TestFixture]
    public class ApiTests
    {
        private DocumentsController _controller = null!;
        private Mock<IDocumentRepository> _mockRepo = null!;
        private Mock<ILogger<DocumentsController>> _mockLogger = null!;
        private Mock<IUserRepository> _mockUserRepo = null!;
        private Mock<IAccessLogService> _mockLogService = null!;
        private Mock<IRabbitMqPublisher> _mockPublisher = null!;
        private Mock<ElasticsearchClient> _mockElastic = null!;
        private Mock<IMinioClient> _mockMinio = null;

        [SetUp]
        public void SetUp()
        {
            _mockRepo = new Mock<IDocumentRepository>();
            _mockLogger = new Mock<ILogger<DocumentsController>>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockLogService = new Mock<IAccessLogService>();
            _mockPublisher = new Mock<IRabbitMqPublisher>();
            _mockElastic = new Mock<ElasticsearchClient>();
            _mockMinio = new Mock<IMinioClient>();


            _mockElastic = new Mock<ElasticsearchClient>();

            _controller = new DocumentsController(
                _mockLogger.Object,
                _mockRepo.Object,
                _mockUserRepo.Object,
                _mockLogService.Object,
                _mockPublisher.Object,
                _mockElastic.Object,
                _mockMinio.Object
            );
        }

        // UPLOAD tests

        [Test]
        public async Task Upload_FileIsNull_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.Upload(null!, "testuser");

            // Assert
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }


        [Test]
        public async Task Upload_ValidFile_SavesToRepositoryAndReturnsCreated()
        {
            // Arrange
            var content = "dummy content";
            var fileName = "test.pdf";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var formFile = new FormFile(stream, 0, stream.Length, "file", fileName);
            var testUser = new User { Id = 1, Username = "testuser" };

            _mockMinio.Setup(m => m.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Document>()))
                     .ReturnsAsync((Document doc) => doc);
            _mockUserRepo.Setup(u => u.GetOrCreateUserAsync("testuser"))
                     .ReturnsAsync(testUser);

            var result = await _controller.Upload(formFile, "testuser");

            if (result is ObjectResult obj && obj.StatusCode == 500)
            {
                Assert.Fail($"Controller crashed with 500: {obj.Value}");
            }

            Assert.That(result, Is.TypeOf<CreatedResult>());
        }


        // SEARCH tests

        [Test]
        public async Task Search_QueryIsEmpty_ReturnsAllDocsFromRepository()
        {
            // Arrange
            var docs = new List<Document> { new Document { Id = 1, FileName = "Test.pdf" } };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(docs);

            // Act
            var result = await _controller.Search("");

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            _mockRepo.Verify(r => r.GetAllAsync(), Times.Once);
        }


        [Test]
        public async Task Search_ValidQuery_ReturnsOkWithResults()
        {
            var documents = new List<Document>
            {
                new Document { Id = 1, FileName = "Report1.pdf", Summary = "Finance report" }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(documents);

            var result = await _controller.Search("report");
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
        }

        // DELETE tests

        [Test]
        public async Task Delete_IdIsNull_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.Delete(null);

            // Assert
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Delete_ValidId_ReturnsOk()
        {
            // Arrange
            int docId = 1;
            _mockRepo.Setup(r => r.DeleteAsync(docId)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Delete(docId);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            _mockRepo.Verify(r => r.DeleteAsync(docId), Times.Once);
        }

        // UPDATE tests

        [Test]
        public async Task Update_InvalidDto_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.Update(null!);

            // Assert
            Assert.That(result, Is.TypeOf<BadRequestResult>());
        }

        [Test]
        public async Task Update_ValidDto_CallsRepositoryAndReturnsOk()
        {
            // Arrange
            var dto = new DocumentsController.UpdateDocumentDto
            {
                DocumentId = 1,
                Content = "New Summary"
            };

            // Act
            var result = await _controller.Update(dto);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            _mockRepo.Verify(r => r.UpdateAsync(dto.DocumentId, dto.Content), Times.Once);
        }


        // summaries endpoint tests
        [Test]
        public async Task GenerateSummary_ValidRequest_ReturnsAcceptedAndPublishesMessage()
        {
            // Arrange
            int docId = 1;
            var doc = new Document
            {
                Id = docId,
                FileName = "test.pdf",
                Summary = ""
            };

            _mockRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(doc);


            _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.GenerateSummary(docId);

            // Assert
            Assert.That(result, Is.TypeOf<AcceptedResult>());


            _mockPublisher.Verify(p => p.PublishAsync(
                RabbitMqQueues.OCR_QUEUE,
                It.Is<string>(s => s.Contains($"\"DocumentId\":{docId}"))
            ), Times.Once);
        }

        [Test]
        public async Task GenerateSummary_AlreadyHasSummary_ReturnsBadRequest()
        {
            // Arrange
            int docId = 1;
            var doc = new Document
            {
                Id = docId,
                FileName = "test.pdf",
                Summary = "This document already has a summary."
            };

            _mockRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(doc);

            // Act
            var result = await _controller.GenerateSummary(docId);

            // Assert
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());

            _mockPublisher.Verify(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task GenerateSummary_DocumentNotFound_ReturnsNotFound()
        {
            // Arrange
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Document?)null);

            // Act
            var result = await _controller.GenerateSummary(999);

            // Assert
            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
        }


        // GET BY ID tests

        [Test]
        public async Task GetById_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            _mockRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Document?)null);

            // Act
            var result = await _controller.GetById(99);

            // Assert
            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        // accesses log tests
        [Test]
        public async Task StoreBatchAccess_ValidData_ReturnsOk()
        {
            // Arrange
            var dtos = new List<DailyAccessDto>
            {
                new DailyAccessDto { DocumentId = 1, AccessCount = 5, Date = DateOnly.FromDateTime(DateTime.Now) }
            };
            _mockLogService.Setup(s => s.StoreBatchAsync(dtos)).ReturnsAsync(true);

            // Act
            var result = await _controller.StoreBatchAccess(dtos);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            var okResult = result as OkObjectResult;

            Assert.That(okResult!.Value!.ToString()!.Contains("1"), Is.True);
        }

        [Test]
        public async Task StoreBatchAccess_ServiceFails_ReturnsBadRequest()
        {
            // Arrange
            var dtos = new List<DailyAccessDto> { new DailyAccessDto { DocumentId = 999 } };
            _mockLogService.Setup(s => s.StoreBatchAsync(dtos)).ReturnsAsync(false);

            // Act
            var result = await _controller.StoreBatchAccess(dtos);

            // Assert
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }
    }
}