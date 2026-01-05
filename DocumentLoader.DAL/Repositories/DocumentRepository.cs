using DocumentLoader.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System;

namespace DocumentLoader.DAL.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly DocumentDbContext _context;
        private readonly ILogger<DocumentRepository> _logger;

        public DocumentRepository(ILogger<DocumentRepository> logger, DocumentDbContext context)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Document> AddAsync(Document doc)
        {
            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();
            return doc;
        }

        public async Task DeleteAsync(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc != null)
            {
                _context.Documents.Remove(doc);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Document?> GetByIdAndDateTimeAsync(int id, DateTime uploadedAt)
        {
            return await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.UploadedAt == uploadedAt);
        }
        public async Task<IEnumerable<Document>> GetAllAsync()
        {
            return await _context.Documents.ToListAsync();
        }

        public async Task<Document?> GetByIdAsync(int id)
        {
            return await _context.Documents.FindAsync(id);
        }

        public async Task UpdateAsync(Document doc)
        {
            _context.Documents.Update(doc);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateSummaryAsync(SummaryResult summary)
        {
            var doc = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == summary.DocumentId
                                       && d.UploadedAt == summary.UploadedAt);

            if (doc != null)
            {
                doc.Summary = summary.SummaryText;
                _logger.LogInformation("Setting doc.Summary = {summary}", summary.SummaryText);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Saved summary for DocumentId {id}", summary.DocumentId);
            }
        }

    }
}
