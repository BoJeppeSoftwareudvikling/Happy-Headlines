using Microsoft.EntityFrameworkCore;
using ProfanityService.Models;

namespace ProfanityService.Data;

public class ProfanityDbContext : DbContext
{
    public ProfanityDbContext(DbContextOptions<ProfanityDbContext> options) : base(options) { }

    public DbSet<ProfanityWord> ProfanityWords => Set<ProfanityWord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProfanityWord>(entity =>
        {
            entity.Property(word => word.Word)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(word => word.Word)
                .IsUnique();
        });
    }
}
