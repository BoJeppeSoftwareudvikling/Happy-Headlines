using ArticleService.Data;
using ArticleService.Models;
using ArticleService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArticleService.Caching;

public sealed class ArticleCacheRefreshWorker(
    ArticleDbContextFactory factory,
    IArticleCache cache,
    IOptions<ArticleCacheOptions> options,
    ILogger<ArticleCacheRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var refreshInterval = TimeSpan.FromSeconds(
            options.Value.RefreshIntervalSeconds);

        await RefreshIfClaimedAsync(refreshInterval, stoppingToken);

        using var timer = new PeriodicTimer(refreshInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshIfClaimedAsync(refreshInterval, stoppingToken);
        }
    }

    private async Task RefreshIfClaimedAsync(
        TimeSpan refreshInterval,
        CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;

            if (!await cache.TryClaimRefreshSlotAsync(
                    now,
                    refreshInterval))
            {
                return;
            }

            var cutoff = now.AddDays(-options.Value.WindowDays);

            await using var db = factory.Create(Continent.Global);

            var articles = await db.Articles
                .AsNoTracking()
                .Where(article => article.PublishedAtUtc >= cutoff)
                .OrderByDescending(article => article.PublishedAtUtc)
                .ToListAsync(cancellationToken);

            await cache.ReplaceAsync(articles);

            logger.LogInformation(
                "ArticleCache refreshed with {ArticleCount} global articles published since {Cutoff}.",
                articles.Count,
                cutoff);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "ArticleCache refresh failed. The previous cache remains active.");
        }
    }
}
