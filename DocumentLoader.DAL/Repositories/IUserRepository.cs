using DocumentLoader.Models;

namespace DocumentLoader.DAL.Repositories
{
    public interface IUserRepository
    {
        Task<User> AddAsync(User user);
        Task DeleteAsync(int id);
        Task<IEnumerable<User>> GetAllAsync();
        Task<User?> GetByIdAsync(int id);
        Task UpdateAsync(int id, string username);
        Task<User> GetOrCreateUserAsync(string username);
    }
}
