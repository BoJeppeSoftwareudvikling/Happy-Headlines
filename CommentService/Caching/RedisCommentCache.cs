using System.Globalization;
using System.Text.Json;
using CommentService.Models;
using Monitoring;
using StackExchange.Redis;

namespace CommentService.Caching;

public sealed class RedisCommentCache(
    IConnectionMultiplexer redis,
    ILogger<RedisCommentCache> logger) : ICommentCache
{
    private const int Capacity = 30;

    private const string LruKey =
        "happy-headlines:{comments}:lru";

    private const string SequenceKey =
        "happy-headlines:{comments}:sequence";

    private const string HitsKey =
        "happy-headlines:{comments}:hits";

    private const string MissesKey =
        "happy-headlines:{comments}:misses";

    private const string GetScript =
        """
        local value = redis.call('GET', KEYS[1])

        if value then
            local sequence = redis.call('INCR', KEYS[3])
            redis.call('ZADD', KEYS[2], sequence, KEYS[1])
            redis.call('INCR', KEYS[4])
            return value
        end

        redis.call('ZREM', KEYS[2], KEYS[1])
        redis.call('INCR', KEYS[5])
        return false
        """;

    private const string SetAndTrimScript =
        """
        redis.call('SET', KEYS[1], ARGV[1])

        local sequence = redis.call('INCR', KEYS[3])
        redis.call('ZADD', KEYS[2], sequence, KEYS[1])

        local count = redis.call('ZCARD', KEYS[2])
        local overflow = count - tonumber(ARGV[2])

        if overflow > 0 then
            local victims = redis.call(
                'ZRANGE',
                KEYS[2],
                0,
                overflow - 1)

            for _, victim in ipairs(victims) do
                redis.call('DEL', victim)
                redis.call('ZREM', KEYS[2], victim)
            end
        end

        return redis.call('ZCARD', KEYS[2])
        """;

    private const string RemoveScript =
        """
        redis.call('DEL', KEYS[1])
        redis.call('ZREM', KEYS[2], KEYS[1])
        return 1
        """;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private IDatabase Database => redis.GetDatabase();

    public async Task<IReadOnlyList<Comment>?> GetAsync(
        int articleId)
    {
        RedisKey entryKey = EntryKey(articleId);

        try
        {
            var result = await Database.ScriptEvaluateAsync(
                GetScript,
                [
                    entryKey,
                    LruKey,
                    SequenceKey,
                    HitsKey,
                    MissesKey
                ]);

            if (result.IsNull)
            {
                return null;
            }

            return JsonSerializer.Deserialize<List<Comment>>(
                result.ToString(),
                JsonOptions);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(
                ex,
                "CommentCache is unavailable. Falling back to CommentDatabase.");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "CommentCache contained invalid JSON for article {ArticleId}.",
                articleId);
            return null;
        }
    }

    public async Task SetAsync(
        int articleId,
        IReadOnlyCollection<Comment> comments)
    {
        RedisKey entryKey = EntryKey(articleId);

        try
        {
            var serialized = JsonSerializer.Serialize(
                comments,
                JsonOptions);

            await Database.ScriptEvaluateAsync(
                SetAndTrimScript,
                [entryKey, LruKey, SequenceKey],
                [serialized, Capacity]);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(
                ex,
                "Could not populate CommentCache for article {ArticleId}.",
                articleId);
        }
    }

    public async Task RemoveAsync(int articleId)
    {
        RedisKey entryKey = EntryKey(articleId);

        try
        {
            await Database.ScriptEvaluateAsync(
                RemoveScript,
                [entryKey, LruKey]);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(
                ex,
                "Could not invalidate CommentCache for article {ArticleId}.",
                articleId);
        }
    }

    public async Task<CacheMetricsSnapshot> GetMetricsAsync()
    {
        var values = await Database.StringGetAsync(
            [HitsKey, MissesKey]);

        var entries = await Database.SortedSetLengthAsync(LruKey);

        return new CacheMetricsSnapshot(
            ParseLong(values[0]),
            ParseLong(values[1]),
            entries);
    }

    private static RedisKey EntryKey(int articleId) =>
        $"happy-headlines:{{comments}}:article:{articleId}";

    private static long ParseLong(RedisValue value) =>
        long.TryParse(
            value.ToString(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;
}
