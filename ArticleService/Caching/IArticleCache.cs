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

    Task<CacheMetricsSnapshot> GetMetricsAsync();
}
