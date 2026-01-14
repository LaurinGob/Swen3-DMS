using DocumentLoader.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;

namespace DocumentLoader.DAL.Repositories
{
    public class AccessLogRepository : IAccessLogRepository
    {
        private readonly DocumentDbContext _db;
        private readonly ILogger<AccessLogRepository> _logger;

        public AccessLogRepository(ILogger<AccessLogRepository> logger, DocumentDbContext db)
        {
            _logger = logger;
            _db = db; 
        }

        public async Task<DailyAccess?> GetAsync(int documentId, DateOnly date)
        {
            return await _db.DailyAccesses
                .FirstOrDefaultAsync(d => d.DocumentId == documentId && d.Date == date);
        }

        public async Task AddAsync(DailyAccess entity)
        {
            _db.DailyAccesses.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(DailyAccess entity)
        {
            _db.DailyAccesses.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpsertAsync(DailyAccess entity)
        {
            try
            {
                var existing = await GetAsync(entity.DocumentId, entity.Date);

                if (existing != null)
                {
                    // Update the existing record
                    _logger.LogInformation("Updating DailyAccess for DocumentId {DocumentId} on {Date}", entity.DocumentId, entity.Date);
                    existing.AccessCount = entity.AccessCount;
                    _db.DailyAccesses.Update(existing);
                }
                else
                {
                    // Insert new record
                    _logger.LogInformation("Inserting new DailyAccess for DocumentId {DocumentId} on {Date}", entity.DocumentId, entity.Date);
                    _db.DailyAccesses.Add(entity);
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during UpsertAsync for DocumentId {DocumentId} on {Date}", entity.DocumentId, entity.Date);
                throw;
            }
        }
    }
}
