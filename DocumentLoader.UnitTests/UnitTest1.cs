using DocumentLoader.API.Controllers;
using DocumentLoader.DAL;
using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using DocumentLoader.Core.Services; // Neu für IAccessLogService
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Neu für ILogger
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Alias für Document, um Mehrdeutigkeiten zu vermeiden
using Document = DocumentLoader.Models.Document;

namespace DocumentLoader.UnitTests
{
    public class Tests
    {
        private DocumentsController _controller = null!;
        private DocumentRepository _repository = null!;
        private DocumentDbContext _context = null!;

        // Mocks für den Controller
        private Mock<IDocumentRepository> _mockRepo = null!;
        private Mock<ILogger<DocumentsController>> _mockControllerLogger = null!;
        private Mock<IAccessLogService> _mockAccessLogService = null!;

        // Mock für das Repository
        private Mock<ILogger<DocumentRepository>> _mockRepoLogger = null!;

        [SetUp]
        public void SetUp()
        {
            // 1. Mocks initialisieren
            _mockRepo = new Mock<IDocumentRepository>();
            _mockControllerLogger = new Mock<ILogger<DocumentsController>>();
            _mockAccessLogService = new Mock<IAccessLogService>();
            _mockRepoLogger = new Mock<ILogger<DocumentRepository>>();

            // 2. Controller initialisieren (mit allen 3 Parametern)
            _controller = new DocumentsController(
                _mockControllerLogger.Object,
                _mockRepo.Object,
                _mockAccessLogService.Object
            );

            // 3. In-Memory Datenbank für Repository-Tests
            var options = new DbContextOptionsBuilder<DocumentDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new DocumentDbContext(options);

            // 4. Repository initialisieren (mit beiden Parametern: Logger & Context)
            _repository = new DocumentRepository(_mockRepoLogger.Object, _context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
        }

        [Test]
        public async Task Upload_ValidFile_SavesToRepositoryAndReturnsCreated()
        {
            // Arrange
            var content = "dummy content";
            var fileName = "test.txt";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var formFile = new FormFile(stream, 0, stream.Length, "file", fileName);

            // Mock für den MinioClient Parameter der Upload-Methode
            var mockMinio = new Mock<Minio.IMinioClient>();

            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Document>()))
                     .ReturnsAsync((Document doc) => doc);

            // Act - Hier muss das zweite Argument (Minio) mitgegeben werden
            var result = await _controller.Upload(formFile, (Minio.MinioClient)mockMinio.Object);

            // Assert
            var createdResult = result as CreatedResult;
            Assert.That(createdResult, Is.Not.Null);

            // Zugriff auf DTOs über den Klassennamen des Controllers
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Document>()), Times.Once);
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

        // --- Repository Tests ---

        [Test]
        public async Task AddAsync_ShouldAddDocumentToDb()
        {
            var doc = new Document { FileName = "test.pdf", Summary = "Test" };
            await _repository.AddAsync(doc);

            var saved = await _context.Documents.FirstOrDefaultAsync(d => d.FileName == "test.pdf");
            Assert.That(saved, Is.Not.Null);
        }
    }
}