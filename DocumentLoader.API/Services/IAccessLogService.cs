namespace DocumentLoader.API.Services
{
    public interface IAccessLogService
    {

        Task StoreDailyAsync(DateOnly date, int documentId, int accessCount);

    }
}
