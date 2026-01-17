using DocumentLoader.DAL.Repositories;
using DocumentLoader.Models;
using Microsoft.Extensions.Logging;

namespace DocumentLoader.Core.Services
{
    public class AccessLogService : IAccessLogService
    {
        private readonly IAccessLogRepository _repository;
        private readonly IDocumentRepository _documentRepo;
        private readonly ILogger<AccessLogService> _logger;

        public AccessLogService(
            IAccessLogRepository repository,
            IDocumentRepository documentRepo,
            ILogger<AccessLogService> logger)
        {
            _repository = repository;
            _documentRepo = documentRepo;
            _logger = logger;
        }

        public async Task StoreDailyAsync(DateOnly date, int documentId, int accessCount)
        {
            if (accessCount < 0)
                throw new ArgumentException("Access count must be non-negative");

            // Optional: validate that document exists
            var document = await _documentRepo.GetByIdAsync(documentId);
            if (document == null)
            throw new InvalidOperationException($"Document {documentId} not found");


            // Idempotent upsert
            var existing = await _repository.GetAsync(documentId, date);
            if (existing != null)
            {
                existing.AccessCount = accessCount; // overwrite
                await _repository.UpdateAsync(existing);
            }
            else
            {
                var newEntry = new DailyAccess
                {
                    DocumentId = documentId,
                    Date = date,
                    AccessCount = accessCount
                };
                _logger.LogInformation("Storing new daily access log for DocumentId {DocumentId} on {Date} with count {AccessCount}",
                    documentId, date, accessCount);
                await _repository.AddAsync(newEntry);

            }
        }

        public async Task<bool> StoreBatchAsync(List<DailyAccessDto> dtos)
        {
            if (dtos == null || !dtos.Any()) return true;

            try
            {
                // 1. Validation Phase: Check if all documents exist first
                // This prevents partial saves without needing complex transactions yet
                foreach (var dto in dtos)
                {
                    var document = await _documentRepo.GetByIdAsync(dto.DocumentId);
                    if (document == null)
                    {
                        _logger.LogWarning("Batch failed: Document {DocumentId} not found", dto.DocumentId);
                        return false;
                    }
                }

                // 2. Execution Phase: All IDs are valid, now upsert them
                foreach (var dto in dtos)
                {
                    await StoreDailyAsync(dto.Date, dto.DocumentId, dto.AccessCount);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during batch access log storage");
                return false;
            }
        }
    }
}
