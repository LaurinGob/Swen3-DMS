using DocumentLoader.API.Controllers;
using DocumentLoader.DAL.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentLoader.UnitTests
{
    public class ApiTests
    {
        private DocumentsController _controller = null!;
        private Mock<IDocumentRepository> _mockRepo = null!;


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
        public async Task Search_EmptyQuery_ReturnsBadRequest()
        {
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
