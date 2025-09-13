using DocumentLoader.Models;
using Microsoft.EntityFrameworkCore;


namespace DocumentLoader.DAL
{
    public class DocumentDbContext : DbContext
    {
        public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options) { }

        public DbSet<Document> Documents { get; set; }
    }
}