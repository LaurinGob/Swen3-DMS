using DocumentLoader.Models;

namespace DocumentLoader.DAL.Repositories
{
    public interface IDocumentRepository
    {
        Task<Document> AddAsync(Document doc);
        Task<Document?> GetByIdAsync(int id);
        Task<IEnumerable<Document>> GetAllAsync();
        Task UpdateAsync(Document doc);
        Task UpdateSummaryAsync(SummaryResult summary);
        Task DeleteAsync(int id);
    }
}
