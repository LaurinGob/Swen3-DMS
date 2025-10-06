
using DocumentLoader.API.Controllers;
using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static DocumentLoader.API.Controllers.DocumentsController;
using Document = DocumentLoader.Models.Document;


namespace DocumentLoader.UnitTests
{
    public class Tests
    {

        private Mock<IDocumentRepository> _mockRepo = null!;
        private DocumentsController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _mockRepo = new Mock<IDocumentRepository>();
            _controller = new DocumentsController(_mockRepo.Object);
        }

        [Test]
        public async Task Upload_NoFile_ReturnsBadRequest()
        {
            IFormFile? file = null;

            var result = await _controller.Upload(file);

            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Upload_ValidFile_SavesToRepositoryAndReturnsCreated()
        {
            var content = "dummy content";
            var fileName = "test.txt";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var formFile = new FormFile(stream, 0, stream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            // Return the same document passed in
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Document>()))
                     .ReturnsAsync((Document doc) => doc);

            var result = await _controller.Upload(formFile);
            var createdResult = result as CreatedResult;

            Assert.That(createdResult, Is.Not.Null);
            var uploadData = createdResult!.Value as UploadResultDto;
            Assert.That(uploadData, Is.Not.Null);
            Assert.That(uploadData!.FileName, Is.EqualTo(fileName));

            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Document>()), Times.Once);
        }



        [Test]
        public async Task Search_EmptyQuery_ReturnsBadRequest()
        {
            var result = await _controller.Search("");
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Search_ValidQuery_ReturnsOkWithResults()
        {
            var documents = new List<Document>
            {
                new Document { Id = 1, FileName = "Report1.pdf", Summary = "Finance report" },
                new Document { Id = 2, FileName = "Notes.txt", Summary = "Meeting notes" }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(documents);

            var result = await _controller.Search("report");
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);

            var data = okResult!.Value as SearchResultDto;
            Assert.That(data, Is.Not.Null);
            Assert.That(data!.Results.Count(), Is.EqualTo(1));
            Assert.That(data.Results.First().FileName, Is.EqualTo("Report1.pdf"));
        }

        [Test]
        public async Task Delete_NullId_ReturnsBadRequest()
        {
            var result = await _controller.Delete(null);
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }
        [Test]
        public async Task Delete_WithValidId_ReturnsOk()
        {
            var result = await _controller.Delete(1);
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult!.Value, Is.EqualTo("Document with the provided id 1 has been deleted"));
        }
        [Test]
        public async Task Update_NullDto_ReturnsBadRequest()
        {
            var result = await _controller.Update(null);
            Assert.That(result, Is.TypeOf<BadRequestResult>());
        }

        [Test]
        public async Task Update_InvalidContent_ReturnsBadRequest()
        {
            var dto = new DocumentsController.UpdateDocumentDto { DocumentId = 1, Content = "" };
            var result = await _controller.Update(dto);
            Assert.That(result, Is.TypeOf<BadRequestResult>());
        }

        [Test]
        public async Task Update_ValidDto_ReturnsOk()
        {
            var dto = new DocumentsController.UpdateDocumentDto
            {
                DocumentId = 1,
                Content = "Updated content"
            };

            var result = await _controller.Update(dto);
            var okResult = result as OkObjectResult;

            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult!.Value, Is.EqualTo("Document with ID 1 has been updated"));
        }
    }
}