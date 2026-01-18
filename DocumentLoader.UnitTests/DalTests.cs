using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentLoader.DAL;
using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace DocumentLoader.UnitTests
{
    internal class DalTests
    {
        private DocumentDbContext _context = null!;
        private AccessLogRepository _accessRepository = null!;
        private DocumentRepository _docRepository = null!;
        private Mock<ILogger<AccessLogRepository>> _mockLogger = null!;
        private Mock<ILogger<DocumentRepository>> _mockDocLogger = null!;

        [SetUp]
        public void SetUp()
        {
            // Erstellt eine frische In-Memory Datenbank für jeden Test
            var options = new DbContextOptionsBuilder<DocumentDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new DocumentDbContext(options);
            _mockLogger = new Mock<ILogger<AccessLogRepository>>();
            _accessRepository = new AccessLogRepository(_mockLogger.Object, _context);
            _mockDocLogger = new Mock<ILogger<DocumentRepository>>();
            _docRepository = new DocumentRepository(_mockDocLogger.Object, _context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        //AccessLogRepository tests

        [Test]
        public async Task GetAsync_ReturnsCorrectEntry()
        {
            // Arrange
            var date = new DateOnly(2024, 5, 20);
            var log = new DailyAccess { DocumentId = 1, Date = date, AccessCount = 100 };
            _context.DailyAccesses.Add(log);
            await _context.SaveChangesAsync();

            // Act
            var result = await _accessRepository.GetAsync(1, date);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.AccessCount, Is.EqualTo(100));
        }

        [Test]
        public async Task UpsertAsync_NewEntry_InsertsIntoDatabase()
        {
            // Arrange
            var log = new DailyAccess { DocumentId = 1, Date = new DateOnly(2024, 1, 1), AccessCount = 50 };

            // Act
            await _accessRepository.UpsertAsync(log);

            // Assert
            var saved = await _context.DailyAccesses.FirstOrDefaultAsync();
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.AccessCount, Is.EqualTo(50));
        }

        [Test]
        public async Task UpsertAsync_ExistingEntry_UpdatesAccessCount()
        {
            // Arrange
            var date = new DateOnly(2024, 1, 1);
            var existing = new DailyAccess { DocumentId = 1, Date = date, AccessCount = 10 };
            _context.DailyAccesses.Add(existing);
            await _context.SaveChangesAsync();

            // Wir lösen das Tracking-Problem für den Test (Detach), damit Upsert frisch laden kann
            _context.Entry(existing).State = EntityState.Detached;

            var updateData = new DailyAccess { DocumentId = 1, Date = date, AccessCount = 99 };

            // Act
            await _accessRepository.UpsertAsync(updateData);

            // Assert
            var result = await _context.DailyAccesses.FirstAsync(d => d.DocumentId == 1 && d.Date == date);
            Assert.That(result.AccessCount, Is.EqualTo(99));
        }

        //DocumentRepository tests

        [Test]
        public async Task AddAsync_SavesDocumentAndSetsId()
        {
            // Arrange
            var doc = new Document { FileName = "test.pdf", FilePath = "minio://uploads/test.pdf" };

            // Act
            var result = await _docRepository.AddAsync(doc);

            // Assert
            Assert.That(result.Id, Is.GreaterThan(0));
            var savedDoc = await _context.Documents.FindAsync(result.Id);
            Assert.That(savedDoc!.FileName, Is.EqualTo("test.pdf"));
        }

        [Test]
        public async Task UpdateAsync_OnlyChangesSummary_KeepsFileName()
        {
            // Arrange
            var doc = new Document { FileName = "important.pdf", Summary = "Old Summary" };
            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();
            _context.Entry(doc).State = EntityState.Detached; // Tracking lösen für sauberen Test

            // Act
            await _docRepository.UpdateAsync(doc.Id, "New Summary");

            // Assert
            var updated = await _context.Documents.FindAsync(doc.Id);
            Assert.That(updated!.Summary, Is.EqualTo("New Summary"));
            Assert.That(updated.FileName, Is.EqualTo("important.pdf")); // WICHTIG: Name darf nicht weg sein!
        }

        [Test]
        public async Task UpdateSummaryAsync_ValidSummaryResult_UpdatesCorrectly()
        {
            // Arrange
            var doc = new Document { FileName = "ocr_test.pdf", Summary = "" };
            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            var summaryResult = new SummaryResult
            {
                DocumentId = doc.Id,
                SummaryText = "AI Generated Content"
            };

            // Act
            await _docRepository.UpdateSummaryAsync(summaryResult);

            // Assert
            var updated = await _context.Documents.FindAsync(doc.Id);
            Assert.That(updated!.Summary, Is.EqualTo("AI Generated Content"));
        }

        [Test]
        public async Task DeleteAsync_ExistingId_RemovesDocument()
        {
            // Arrange
            var doc = new Document { FileName = "delete_me.pdf" };
            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            // Act
            await _docRepository.DeleteAsync(doc.Id);

            // Assert
            var result = await _context.Documents.FindAsync(doc.Id);
            Assert.That(result, Is.Null);
        }
    }



}

