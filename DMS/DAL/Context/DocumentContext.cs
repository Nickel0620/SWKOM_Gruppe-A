using DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace DAL.Context
{
    public class DocumentContext : DbContext
    {
        public DocumentContext(DbContextOptions<DocumentContext> options) : base(options) { }

        // Parameterless constructor for design-time tools
        public DocumentContext() : base(new DbContextOptionsBuilder<DocumentContext>().UseNpgsql("Host=localhost;Database=dmsdb;Username=dmsuser;Password=dmspassword").Options)
        {
        }

        public DbSet<Document> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the Document entity
            modelBuilder.Entity<Document>()
                .Property(d => d.Id)
                .ValueGeneratedOnAdd(); // Ensure auto-generation

            modelBuilder.Entity<Document>()
                .Property(d => d.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP"); // Default CreatedAt

            modelBuilder.Entity<Document>()
                .Property(d => d.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP") // Default UpdatedAt
                .ValueGeneratedOnUpdate(); // Automatically update on modifications
        }
    }
}