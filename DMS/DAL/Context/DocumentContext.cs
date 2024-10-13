using Microsoft.EntityFrameworkCore;
using DAL.Entities;

namespace DAL.Context
{
    public class DocumentContext : DbContext
    {
        public DocumentContext(DbContextOptions<DocumentContext> options) : base(options) { }

        public DbSet<Document> Documents { get; set; }
    }
}
