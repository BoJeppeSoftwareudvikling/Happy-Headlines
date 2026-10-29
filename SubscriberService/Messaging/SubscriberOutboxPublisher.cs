using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using SubscriberService.Data;
using SubscriberService.Features;

namespace SubscriberService.Messaging;

public sealed class SubscriberOutboxPublisher(
    SubscriberDbContextFactory factory,
    SubscriberPublisher publisher,
    ISubscriberReleaseToggle releaseToggle,
    ILogger<SubscriberOutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!releaseToggle.IsEnabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            try
            {
                var publishedAny = await PublishBatchAsync(stoppingToken);
                if (!publishedAny)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Could not publish subscriber outbox messages. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<bool> PublishBatchAsync(CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var messages = await db.OutboxMessages
            .Where(message => message.PublishedAtUtc == null)
            .OrderBy(message => message.SubscribedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        var publishedAny = false;
        foreach (var message in messages)
        {
            if (!releaseToggle.IsEnabled)
            {
                break;
            }

            var contract = new SubscriberRegistered
            {
                MessageId = message.MessageId,
                SubscriberId = message.SubscriberId,
                Email = message.Email,
                SubscribedAtUtc = message.SubscribedAtUtc
            };

            await publisher.PublishAsync(contract, cancellationToken);
            message.PublishedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            publishedAny = true;

            logger.LogInformation(
                "Published subscriber {SubscriberId} as message {MessageId}.",
                message.SubscriberId,
                message.MessageId);
        }

        return publishedAny;
    }
}
