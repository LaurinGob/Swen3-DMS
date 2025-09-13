using DocumentLoader.Models;

namespace DocumentLoader.DAL.Repositories
{
    public interface IDocumentRepository
    {
        Task<Document> AddAsync(Document doc);
        Task<Document?> GetByIdAsync(Guid id);
        Task<IEnumerable<Document>> GetAllAsync();
        Task UpdateAsync(Document doc);
        Task DeleteAsync(Guid id);
    }
}
