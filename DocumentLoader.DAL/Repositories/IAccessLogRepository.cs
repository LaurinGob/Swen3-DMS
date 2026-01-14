using DocumentLoader.Models;

namespace DocumentLoader.DAL.Repositories
{
    public interface IAccessLogRepository
    {
        Task<DailyAccess?> GetAsync(int documentId, DateOnly date);
        Task AddAsync(DailyAccess entity);
        Task UpdateAsync(DailyAccess entity);
        Task UpsertAsync(DailyAccess entity);

    }
}
