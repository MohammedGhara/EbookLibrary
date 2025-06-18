using Microsoft.EntityFrameworkCore;
using OnlineLibrary.Models;

namespace OnlineLibrary.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DbSet for Books table
        public DbSet<Book> Books { get; set; }

        // Configure the schema (optional, if you need to map specific table names or properties)
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Book>(entity =>
            {
                entity.HasKey(b => b.BookID); // Set BookID as Primary Key
                entity.Property(b => b.BookID).ValueGeneratedOnAdd(); // Auto-increment
                entity.Property(b => b.Title).IsRequired().HasMaxLength(255);
                entity.Property(b => b.Author).IsRequired().HasMaxLength(255);
                entity.Property(b => b.Price).HasColumnType("decimal(18, 2)");
            });
        }

    }
}
