using DocumentLoader.Models;

namespace DocumentLoader.Core.Services
{
    public interface IAccessLogService
    {

        Task StoreDailyAsync(DateOnly date, int documentId, int accessCount);
        Task<bool> StoreBatchAsync(List<DailyAccessDto> dtos);

    }
}
