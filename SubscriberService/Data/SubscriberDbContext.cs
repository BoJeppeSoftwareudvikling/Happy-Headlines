using Microsoft.EntityFrameworkCore;
using SubscriberService.Models;

namespace SubscriberService.Data;

public class SubscriberDbContext(DbContextOptions<SubscriberDbContext> options)
    : DbContext(options)
{
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<SubscriberOutboxMessage> OutboxMessages => Set<SubscriberOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscriber>(entity =>
        {
            entity.HasKey(subscriber => subscriber.Id);
            entity.Property(subscriber => subscriber.Email).HasMaxLength(320);
            entity.Property(subscriber => subscriber.NormalizedEmail).HasMaxLength(320);
            entity.HasIndex(subscriber => subscriber.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<SubscriberOutboxMessage>(entity =>
        {
            entity.HasKey(message => message.MessageId);
            entity.Property(message => message.Email).HasMaxLength(320);
            entity.HasIndex(message => new { message.PublishedAtUtc, message.SubscribedAtUtc });
        });
    }
}
