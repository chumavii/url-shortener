using Microsoft.EntityFrameworkCore;
using UrlShortener.Models;

namespace UrlShortener.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<UrlMapping> UrlMappings { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UrlMapping>()
                .Property(x => x.OriginalUrl)
                .IsRequired();
            modelBuilder.Entity<UrlMapping>()
                .HasIndex(c => c.OriginalUrl)
                .IsUnique();

            modelBuilder.Entity<UrlMapping>()
                .Property(x => x.ShortCode)
                .IsRequired();
            modelBuilder.Entity<UrlMapping>()
                .HasIndex(c => c.ShortCode)
                .IsUnique();
        }
    }
}
