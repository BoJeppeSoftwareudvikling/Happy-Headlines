
using CommentService.Models;
using Microsoft.EntityFrameworkCore;

namespace CommentService.Data;

public class CommentDbContext : DbContext
{
    public CommentDbContext(DbContextOptions<CommentDbContext> options) : base(options) { }

    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.Property(comment => comment.AuthorName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(comment => comment.Content)
                .HasMaxLength(2000)
                .IsRequired();

            entity.HasIndex(comment => new
            {
                comment.ArticleId,
                comment.Continent,
                comment.CreatedAtUtc
            });
        });
    }
}
