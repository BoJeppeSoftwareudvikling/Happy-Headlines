using DraftService.Models;
using Microsoft.EntityFrameworkCore;

namespace DraftService.Data;

public class DraftDbContext(DbContextOptions<DraftDbContext> options) : DbContext(options)
{
    public DbSet<Draft> Drafts => Set<Draft>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var draft = modelBuilder.Entity<Draft>();
        draft.Property(item => item.PublisherId).HasMaxLength(100).IsRequired();
        draft.Property(item => item.Title).HasMaxLength(200).IsRequired();
        draft.Property(item => item.Content).IsRequired();
        draft.HasIndex(item => item.PublisherId);
    }
}
