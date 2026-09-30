using System.Globalization;
using System.Text.Json;
using ArticleCache.Contracts;
using Monitoring;
using StackExchange.Redis;

namespace ArticleService.Caching;

public sealed class RedisArticleCache(
    IConnectionMultiplexer connection,
    ILogger<RedisArticleCache> logger) : IArticleCache
{
    private const string CacheName = "article";

    public async Task<ArticleCacheReadResult<CachedArticle>> GetByIdAsync(
        int articleId,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var database = connection.GetDatabase();
            var activeVersion = await database.StringGetAsync(
                ArticleCacheKeys.ActiveVersion);

            if (!activeVersion.HasValue)
            {
                CacheMetrics.RecordMiss(CacheName);
                return ArticleCacheReadResult<CachedArticle>.Miss();
            }

            var articleField = articleId.ToString(
                CultureInfo.InvariantCulture);

            var value = await database.HashGetAsync(
                ArticleCacheKeys.SnapshotItems(activeVersion.ToString()),
                articleField);

            if (!value.HasValue)
            {
                CacheMetrics.RecordMiss(CacheName);
                return ArticleCacheReadResult<CachedArticle>.Miss();
            }

            try
            {
                var article = Deserialize(value);
                CacheMetrics.RecordHit(CacheName);
                return ArticleCacheReadResult<CachedArticle>.Hit(article);
            }
            catch (JsonException exception)
            {
                logger.LogError(
                    exception,
                    "Could not deserialize cached article {ArticleId} " +
                    "from snapshot {Version}.",
                    articleId,
                    activeVersion);

                await database.HashDeleteAsync(
                    ArticleCacheKeys.SnapshotItems(activeVersion.ToString()),
                    articleField);

                CacheMetrics.RecordError(CacheName);
                return ArticleCacheReadResult<CachedArticle>.Error();
            }
        }
        catch (RedisException exception)
        {
            logger.LogError(
                exception,
                "ArticleCache lookup failed for article {ArticleId}.",
                articleId);

            CacheMetrics.RecordError(CacheName);
            return ArticleCacheReadResult<CachedArticle>.Error();
        }
    }

    public async Task<ArticleCacheReadResult<IReadOnlyList<CachedArticle>>>
        GetRecentAsync(
            int limit,
            CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var database = connection.GetDatabase();
            var activeVersion = await database.StringGetAsync(
                ArticleCacheKeys.ActiveVersion);

            if (!activeVersion.HasValue)
            {
                CacheMetrics.RecordMiss(CacheName);
                return ArticleCacheReadResult<
                    IReadOnlyList<CachedArticle>>.Miss();
            }

            var version = activeVersion.ToString();
            var articleIds = await database.SortedSetRangeByRankAsync(
                ArticleCacheKeys.SnapshotPublished(version),
                start: 0,
                stop: limit - 1,
                order: Order.Descending);

            if (articleIds.Length == 0)
            {
                CacheMetrics.RecordHit(CacheName);
                return ArticleCacheReadResult<
                    IReadOnlyList<CachedArticle>>.Hit(
                        Array.Empty<CachedArticle>());
            }

            var values = await database.HashGetAsync(
                ArticleCacheKeys.SnapshotItems(version),
                articleIds);

            if (values.Any(value => !value.HasValue))
            {
                logger.LogWarning(
                    "Article snapshot {Version} contained missing hash values.",
                    version);

                CacheMetrics.RecordMiss(CacheName);
                return ArticleCacheReadResult<
                    IReadOnlyList<CachedArticle>>.Miss();
            }

            var articles = new List<CachedArticle>(values.Length);

            for (var index = 0; index < values.Length; index++)
            {
                try
                {
                    articles.Add(Deserialize(values[index]));
                }
                catch (JsonException exception)
                {
                    logger.LogError(
                        exception,
                        "Could not deserialize cached article field " +
                        "{ArticleField} from snapshot {Version}.",
                        articleIds[index],
                        version);

                    await database.HashDeleteAsync(
                        ArticleCacheKeys.SnapshotItems(version),
                        articleIds[index]);

                    CacheMetrics.RecordError(CacheName);
                    return ArticleCacheReadResult<
                        IReadOnlyList<CachedArticle>>.Error();
                }
            }

            CacheMetrics.RecordHit(CacheName);
            return ArticleCacheReadResult<
                IReadOnlyList<CachedArticle>>.Hit(articles);
        }
        catch (RedisException exception)
        {
            logger.LogError(
                exception,
                "ArticleCache recent lookup failed.");

            CacheMetrics.RecordError(CacheName);
            return ArticleCacheReadResult<
                IReadOnlyList<CachedArticle>>.Error();
        }
    }

    public async Task InvalidateAsync(
        int articleId,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var database = connection.GetDatabase();
            var activeVersion = await database.StringGetAsync(
                ArticleCacheKeys.ActiveVersion);

            if (!activeVersion.HasValue)
            {
                CacheMetrics.RecordInvalidation(CacheName, "success");
                return;
            }

            var version = activeVersion.ToString();
            var articleField = articleId.ToString(
                CultureInfo.InvariantCulture);

            var transaction = database.CreateTransaction();

            var deleteItem = transaction.HashDeleteAsync(
                ArticleCacheKeys.SnapshotItems(version),
                articleField);

            var deletePublished = transaction.SortedSetRemoveAsync(
                ArticleCacheKeys.SnapshotPublished(version),
                articleField);

            var committed = await transaction.ExecuteAsync();

            if (!committed)
            {
                logger.LogWarning(
                    "ArticleCache invalidation transaction was not committed " +
                    "for article {ArticleId}.",
                    articleId);

                CacheMetrics.RecordInvalidation(CacheName, "error");
                return;
            }

            await Task.WhenAll(deleteItem, deletePublished);
            CacheMetrics.RecordInvalidation(CacheName, "success");
        }
        catch (RedisException exception)
        {
            logger.LogError(
                exception,
                "ArticleCache invalidation failed for article {ArticleId}.",
                articleId);
            CacheMetrics.RecordInvalidation(CacheName, "error");
        }
    }

    private static CachedArticle Deserialize(RedisValue value) =>
        JsonSerializer.Deserialize<CachedArticle>(value.ToString())
        ?? throw new JsonException("Cached article contained JSON null.");
}