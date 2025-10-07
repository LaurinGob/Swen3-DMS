using DocumentLoader.Models;
using Microsoft.EntityFrameworkCore;

using System;

namespace DocumentLoader.DAL.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly DocumentDbContext _context;

        public DocumentRepository(DocumentDbContext context)
        {
            _context = context;
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
    }
}
