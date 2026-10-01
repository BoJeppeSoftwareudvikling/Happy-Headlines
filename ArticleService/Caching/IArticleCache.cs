using ArticleService.Models;
using Monitoring;

namespace ArticleService.Caching;

public interface IArticleCache
{
    Task<Article?> GetAsync(int articleId);
    Task<IReadOnlyList<Article>?> GetAllAsync();
    Task ReplaceAsync(IReadOnlyCollection<Article> articles);
    Task RemoveAsync(int articleId);

    Task<bool> TryClaimRefreshSlotAsync(
        DateTimeOffset now,
        TimeSpan refreshInterval);

    Task RecordRefreshSuccessAsync(
        DateTimeOffset completedAt,
        TimeSpan duration,
        int loadedEntries);

    Task RecordRefreshFailureAsync();

    Task<CacheMetricsSnapshot> GetMetricsAsync();
}
