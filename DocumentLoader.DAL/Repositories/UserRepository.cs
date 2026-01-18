using DocumentLoader.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace DocumentLoader.DAL.Repositories
{
    public class UserRepository : IUserRepository // Repo for managing User records
    {

        private readonly DocumentDbContext _context;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(ILogger<UserRepository> logger, DocumentDbContext context)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<User> AddAsync(User user)
        {

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task DeleteAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task UpdateAsync(int id, string username)
        {
            var existing = await _context.Users.FindAsync(id);
            if (existing != null)
            {
                existing.Username = username;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<User> GetOrCreateUserAsync(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                user = new User { Username = username };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            return user;
        }
    }
}
