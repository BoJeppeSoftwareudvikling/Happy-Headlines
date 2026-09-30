using ArticleCache.Contracts;

namespace ArticleService.Caching;

public interface IArticleCache
{
    Task<ArticleCacheReadResult<CachedArticle>> GetByIdAsync(
        int articleId,
        CancellationToken cancellationToken);

    Task<ArticleCacheReadResult<IReadOnlyList<CachedArticle>>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken);

    Task InvalidateAsync(
        int articleId,
        CancellationToken cancellationToken);
}