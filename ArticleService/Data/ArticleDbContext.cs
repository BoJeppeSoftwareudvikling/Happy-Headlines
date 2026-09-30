using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data;

public class ArticleDbContext : DbContext
{
    public ArticleDbContext(DbContextOptions<ArticleDbContext> options) : base(options) { }
    public DbSet<Article> Articles => Set<Article>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var article = modelBuilder.Entity<Article>();
        
        article.Property(item => item.PublishedAtUtc)
        .HasColumnType("datetimeoffset");

        article.HasIndex(item => item.PublishedAtUtc)
            .HasDatabaseName("IX_Articles_PublishedAtUtc");

        article.HasIndex(item => item.SourceMessageId)
            .IsUnique()
            .HasFilter("[SourceMessageId] IS NOT NULL");
    }
}
