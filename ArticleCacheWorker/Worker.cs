using Microsoft.Extensions.Options;

namespace ArticleCacheWorker;

public sealed class Worker(
    ArticleSnapshotWriter snapshotWriter,
    IOptions<ArticleCacheRefreshOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await RefreshUntilSuccessfulAsync(stoppingToken);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(options.Value.IntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshUntilSuccessfulAsync(stoppingToken);
        }
    }

    private async Task RefreshUntilSuccessfulAsync(
        CancellationToken stoppingToken)
    {
        while (true)
        {
            try
            {
                await snapshotWriter.RefreshAsync(stoppingToken);
                return;
            }
            catch (Exception exception)
                when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(
                    exception,
                    "ArticleCache refresh failed. Retrying in " +
                    "{RetryDelaySeconds} seconds.",
                    options.Value.RetryDelaySeconds);

                await Task.Delay(
                    TimeSpan.FromSeconds(options.Value.RetryDelaySeconds),
                    stoppingToken);
            }
        }
    }
}