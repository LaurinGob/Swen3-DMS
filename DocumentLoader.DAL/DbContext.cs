using DocumentLoader.Models;
using Microsoft.EntityFrameworkCore;


namespace DocumentLoader.DAL
{
    public class DocumentDbContext : DbContext
    {
        public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options) { }

        public DbSet<Document> Documents { get; set; }
        public DbSet<DailyAccess> DailyAccesses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure composite key for DailyAccess
            modelBuilder.Entity<DailyAccess>()
                .HasKey(d => new { d.DocumentId, d.Date });

            //foreign key to Document 
            modelBuilder.Entity<DailyAccess>()
                .HasOne<Document>()           
                .WithMany()
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}