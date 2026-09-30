using System.Globalization;
using System.Text.Json;
using CommentService.Models;
using Microsoft.Extensions.Options;
using Monitoring;
using StackExchange.Redis;

namespace CommentService.Caching;

public sealed class RedisCommentCache(
    IConnectionMultiplexer connection,
    IOptions<CommentCacheOptions> options,
    ILogger<RedisCommentCache> logger) : ICommentCache
{
    private const string CacheName = "comment";
    private const string DataKeyPrefix =
        "happy-headlines:comment-cache:data:";
    private const string LruKey =
        "happy-headlines:comment-cache:lru";

    public async Task<CommentCacheReadResult<IReadOnlyList<Comment>>> GetAsync(
        int articleId,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var articleField = ToArticleField(articleId);
            var result = await connection.GetDatabase().ScriptEvaluateAsync(
                CommentCacheScripts.HitAndTouch,
                [DataKey(articleField), LruKey],
                [articleField]);

            if (result.IsNull)
            {
                CacheMetrics.RecordMiss(CacheName);
                return CommentCacheReadResult<
                    IReadOnlyList<Comment>>.Miss();
            }

            try
            {
                var comments = JsonSerializer.Deserialize<List<Comment>>(
                    result.ToString())
                    ?? throw new JsonException(
                        "Cached comments contained JSON null.");

                CacheMetrics.RecordHit(CacheName);
                return CommentCacheReadResult<
                    IReadOnlyList<Comment>>.Hit(comments);
            }
            catch (JsonException exception)
            {
                logger.LogError(
                    exception,
                    "Could not deserialize cached comments for article " +
                    "{ArticleId}.",
                    articleId);

                await InvalidateCoreAsync(articleField);
                CacheMetrics.RecordError(CacheName);
                return CommentCacheReadResult<
                    IReadOnlyList<Comment>>.Error();
            }
        }
        catch (RedisException exception)
        {
            logger.LogError(
                exception,
                "CommentCache lookup failed for article {ArticleId}.",
                articleId);

            CacheMetrics.RecordError(CacheName);
            return CommentCacheReadResult<
                IReadOnlyList<Comment>>.Error();
        }
    }

    public async Task StoreAsync(
        int articleId,
        IReadOnlyList<Comment> comments,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var articleField = ToArticleField(articleId);
            var json = JsonSerializer.Serialize(comments);

            var result = await connection.GetDatabase().ScriptEvaluateAsync(
                CommentCacheScripts.StoreAndTrim,
                [LruKey, DataKey(articleField)],
                [
                    articleField,
                    json,
                    options.Value.EntryTtlSeconds,
                    options.Value.MaximumArticles,
                    DataKeyPrefix
                ]);

            var evicted = (RedisResult[]?)result;
            if (evicted is { Length: > 0 })
            {
                CacheMetrics.RecordCommentEviction(evicted.Length);
                logger.LogInformation(
                    "CommentCache evicted article entries {ArticleIds}.",
                    string.Join(", ", evicted.Select(item => item.ToString())));
            }
        }
        catch (RedisException exception)
        {
            logger.LogError(
                exception,
                "Could not cache comments for article {ArticleId}.",
                articleId);
        }
        catch (JsonException exception)
        {
            logger.LogError(
                exception,
                "Could not serialize comments for article {ArticleId}.",
                articleId);
        }
    }

    public async Task InvalidateAsync(
        int articleId,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InvalidateCoreAsync(ToArticleField(articleId));
            CacheMetrics.RecordInvalidation(CacheName, "success");
        }
        catch (RedisException exception)
        {
            logger.LogError(
                exception,
                "CommentCache invalidation failed for article {ArticleId}.",
                articleId);
            CacheMetrics.RecordInvalidation(CacheName, "error");
        }
    }

    private Task<RedisResult> InvalidateCoreAsync(string articleField) =>
        connection.GetDatabase().ScriptEvaluateAsync(
            CommentCacheScripts.Invalidate,
            [DataKey(articleField), LruKey],
            [articleField]);

    private static string DataKey(string articleField) =>
        DataKeyPrefix + articleField;

    private static string ToArticleField(int articleId) =>
        articleId.ToString(CultureInfo.InvariantCulture);
}
