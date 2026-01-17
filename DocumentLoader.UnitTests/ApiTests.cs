using DocumentLoader.API.Controllers;
using DocumentLoader.DAL.Repositories;
using DocumentLoader.Core.Services; // Wichtig für IAccessLogService
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Minio; // Falls MinioClient direkt benötigt wird

namespace DocumentLoader.UnitTests
{
    public class ApiTests
    {
        private DocumentsController _controller = null!;
        private Mock<IDocumentRepository> _mockRepo = null!;
        private Mock<ILogger<DocumentsController>> _mockLogger = null!;
        private Mock<IAccessLogService> _mockLogService = null!;
        private Mock<IMinioClient> _mockMinio = null!; // Falls der Controller das Interface nutzt

        [SetUp]
        public void SetUp()
        {
            // 1. Alle benötigten Abhängigkeiten mocken
            _mockRepo = new Mock<IDocumentRepository>();
            _mockLogger = new Mock<ILogger<DocumentsController>>();
            _mockLogService = new Mock<IAccessLogService>();
            _mockMinio = new Mock<IMinioClient>();

            // 2. Den Controller mit ALLEN Mocks initialisieren
            // Entspricht dem Konstruktor: (ILogger, IDocumentRepository, IAccessLogService)
            _controller = new DocumentsController(
                _mockLogger.Object,
                _mockRepo.Object,
                _mockLogService.Object
            );
        }

        [Test]
        public async Task Upload_NoFile_ReturnsBadRequest()
        {
            // Da die Methode [FromServices] MinioClient erwartet, 
            // müssen wir diesen im Test als Argument übergeben.
            IFormFile? file = null;
            var minioInstance = new Mock<Minio.MinioClient>().Object; // Simpler Mock für den Parameter

            var result = await _controller.Upload(file, minioInstance);

            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Search_EmptyQuery_ReturnsBadRequest()
        {
            // Die Search-Methode braucht keine zusätzlichen Services als Parameter
            var result = await _controller.Search("");
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Delete_NullId_ReturnsBadRequest()
        {
            var result = await _controller.Delete(null);
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }
    }
}