using DocumentLoader.Core.Services;
using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentLoader.UnitTests
{
    internal class CoreTests
    {
        private Mock<IAccessLogRepository> _mockLogRepo = null!;
        private Mock<IDocumentRepository> _mockDocRepo = null!;
        private Mock<ILogger<AccessLogService>> _mockLogger = null!;
        private AccessLogService _service = null!;

    

        [SetUp]
        public void SetUp()
        {
            _mockLogRepo = new Mock<IAccessLogRepository>();
            _mockDocRepo = new Mock<IDocumentRepository>();
            _mockLogger = new Mock<ILogger<AccessLogService>>();

            _service = new AccessLogService(
                _mockLogRepo.Object,
                _mockDocRepo.Object,
                _mockLogger.Object
            );
        }
        //StoreDailyAsync tests
        [Test]
        public void StoreDailyAsync_NegativeCount_ThrowsArgumentException()
        {
            var date = new DateOnly(2024, 1, 1);
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _service.StoreDailyAsync(date, 1, -5));
        }

        [Test]
        public async Task StoreDailyAsync_NewEntry_CallsAddAsync()
        {
            // Arrange
            var date = new DateOnly(2024, 1, 1);
            int docId = 1;
            _mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(new Document { Id = docId });
            _mockLogRepo.Setup(r => r.GetAsync(docId, date)).ReturnsAsync((DailyAccess?)null);

            // Act
            await _service.StoreDailyAsync(date, docId, 10);

            // Assert
            _mockLogRepo.Verify(r => r.AddAsync(It.Is<DailyAccess>(a =>
                a.DocumentId == docId && a.AccessCount == 10)), Times.Once);
        }

        [Test]
        public async Task StoreDailyAsync_ExistingEntry_CallsUpdateAsync()
        {
            // Arrange
            var date = new DateOnly(2024, 1, 1);
            int docId = 1;
            var existingLog = new DailyAccess { DocumentId = docId, Date = date, AccessCount = 5 };

            _mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(new Document { Id = docId });
            _mockLogRepo.Setup(r => r.GetAsync(docId, date)).ReturnsAsync(existingLog);

            // Act
            await _service.StoreDailyAsync(date, docId, 20);

            // Assert
            Assert.That(existingLog.AccessCount, Is.EqualTo(20));
            _mockLogRepo.Verify(r => r.UpdateAsync(existingLog), Times.Once);
            _mockLogRepo.Verify(r => r.AddAsync(It.IsAny<DailyAccess>()), Times.Never);
        }

        // StoreBatchAsync tests

        [Test]
        public async Task StoreBatchAsync_OneDocumentMissing_ReturnsFalseAndAborts()
        {
            // Arrange
            var dtos = new List<DailyAccessDto>
            {
                new DailyAccessDto { DocumentId = 1, Date = new DateOnly(2024,1,1) },
                new DailyAccessDto { DocumentId = 99, Date = new DateOnly(2024,1,1) } 
            };

            _mockDocRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Document { Id = 1 });
            _mockDocRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Document?)null);

            // Act
            var result = await _service.StoreBatchAsync(dtos);

            // Assert
            Assert.That(result, Is.False);
            _mockLogRepo.Verify(r => r.AddAsync(It.IsAny<DailyAccess>()), Times.Never);
        }

        [Test]
        public async Task StoreBatchAsync_AllValid_ReturnsTrue()
        {
            // Arrange
            var dtos = new List<DailyAccessDto>
            {
                new DailyAccessDto { DocumentId = 1, Date = new DateOnly(2024,1,1), AccessCount = 10 }
            };

            _mockDocRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Document { Id = 1 });
            _mockLogRepo.Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<DateOnly>())).ReturnsAsync((DailyAccess?)null);

            // Act
            var result = await _service.StoreBatchAsync(dtos);

            // Assert
            Assert.That(result, Is.True);
            _mockLogRepo.Verify(r => r.AddAsync(It.IsAny<DailyAccess>()), Times.Once);
        }

    }

}
