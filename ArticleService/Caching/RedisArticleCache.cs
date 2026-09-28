using System.Globalization;
using System.Text.Json;
using ArticleService.Models;
using Monitoring;
using StackExchange.Redis;

namespace ArticleService.Caching;

public sealed class RedisArticleCache(
    IConnectionMultiplexer redis,
    ILogger<RedisArticleCache> logger) : IArticleCache
{
    private const string EntriesKey =
        "happy-headlines:{articles}:entries";

    private const string HitsKey =
        "happy-headlines:{articles}:hits";

    private const string MissesKey =
        "happy-headlines:{articles}:misses";

    private const string RefreshMetricsKey =
        "happy-headlines:{articles}:refresh-metrics";

    private const string RefreshFailuresField =
        "failures";

    private const string LastRefreshSuccessField =
        "last-success-unix-seconds";

    private const string LastRefreshDurationField =
        "last-duration-seconds";

    private const string LastRefreshLoadedEntriesField =
        "last-loaded-entries";

    private const string ReadyField = "__ready__";

    private const string GetScript =
        """
        local value = redis.call('HGET', KEYS[1], ARGV[1])
        if value then
            redis.call('INCR', KEYS[2])
            return value
        end

        redis.call('INCR', KEYS[3])
        return false
        """;

    private const string PublishStagingScript =
        """
        redis.call('RENAME', KEYS[1], KEYS[2])
        return 1
        """;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private IDatabase Database => redis.GetDatabase();

    public async Task<Article?> GetAsync(int articleId)
    {
        try
        {
            var result = await Database.ScriptEvaluateAsync(
                GetScript,
                [EntriesKey, HitsKey, MissesKey],
                [articleId]);

            if (result.IsNull)
            {
                return null;
            }

            return JsonSerializer.Deserialize<Article>(
                result.ToString(),
                JsonOptions);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(
                ex,
                "ArticleCache is unavailable. Falling back to ArticleDatabase.");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "ArticleCache contained invalid JSON for article {ArticleId}.",
                articleId);
            return null;
        }
    }

    public async Task<IReadOnlyList<Article>?> GetAllAsync()
    {
        try
        {
            var entries = await Database.HashGetAllAsync(EntriesKey);

            if (entries.Length == 0)
            {
                await Database.StringIncrementAsync(MissesKey);
                return null;
            }

            await Database.StringIncrementAsync(HitsKey);

            return entries
                .Where(entry => entry.Name.ToString() != ReadyField)
                .Select(entry => JsonSerializer.Deserialize<Article>(
                    entry.Value.ToString(),
                    JsonOptions))
                .Where(article => article is not null)
                .Cast<Article>()
                .OrderByDescending(article => article.PublishedAtUtc)
                .ToList();
        }
        catch (RedisException ex)
        {
            logger.LogWarning(
                ex,
                "ArticleCache is unavailable. Falling back to ArticleDatabase.");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "ArticleCache contained invalid article JSON.");
            return null;
        }
    }

    public async Task ReplaceAsync(IReadOnlyCollection<Article> articles)
    {
        var database = Database;
        RedisKey stagingKey =
            $"happy-headlines:{{articles}}:staging:{Guid.NewGuid():N}";

        var entries = new List<HashEntry>
        {
            new(ReadyField, "1")
        };

        entries.AddRange(articles.Select(article =>
            new HashEntry(
                article.Id,
                JsonSerializer.Serialize(article, JsonOptions))));

        try
        {
            await database.HashSetAsync(stagingKey, [.. entries]);

            await database.ScriptEvaluateAsync(
                PublishStagingScript,
                [stagingKey, EntriesKey]);
        }
        catch
        {
            try
            {
                await database.KeyDeleteAsync(stagingKey);
            }
            catch (RedisException)
            {
                // Preserve the original refresh exception.
            }

            throw;
        }
    }

    public async Task RemoveAsync(int articleId)
    {
        try
        {
            await Database.HashDeleteAsync(EntriesKey, articleId);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(
                ex,
                "Could not invalidate article {ArticleId} in ArticleCache.",
                articleId);
        }
    }

    public async Task<bool> TryClaimRefreshSlotAsync(
        DateTimeOffset now,
        TimeSpan refreshInterval)
    {
        var intervalSeconds =
            Math.Max(1L, (long)Math.Ceiling(refreshInterval.TotalSeconds));

        var slot = now.ToUnixTimeSeconds() / intervalSeconds;
        RedisKey slotKey =
            $"happy-headlines:{{articles}}:refresh-slot:{slot}";

        return await Database.StringSetAsync(
            slotKey,
            Environment.MachineName,
            TimeSpan.FromSeconds(intervalSeconds * 2),
            When.NotExists);
    }

    public async Task RecordRefreshSuccessAsync(
        DateTimeOffset completedAt,
        TimeSpan duration,
        int loadedEntries)
    {
        try
        {
            await Database.HashSetAsync(
                RefreshMetricsKey,
                [
                    new HashEntry(
                        LastRefreshSuccessField,
                        completedAt.ToUnixTimeSeconds()),
                    new HashEntry(
                        LastRefreshDurationField,
                        duration.TotalSeconds),
                    new HashEntry(
                        LastRefreshLoadedEntriesField,
                        loadedEntries)
                ]);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(
                ex,
                "Could not record successful ArticleCache refresh metrics.");
        }
    }

    public async Task RecordRefreshFailureAsync()
    {
        try
        {
            await Database.HashIncrementAsync(
                RefreshMetricsKey,
                RefreshFailuresField);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(
                ex,
                "Could not record failed ArticleCache refresh metrics.");
        }
    }

    public async Task<CacheMetricsSnapshot> GetMetricsAsync()
    {
        var values = await Database.StringGetAsync(
            [HitsKey, MissesKey]);

        var refreshValues = await Database.HashGetAsync(
            RefreshMetricsKey,
            [
                RefreshFailuresField,
                LastRefreshSuccessField,
                LastRefreshDurationField,
                LastRefreshLoadedEntriesField
            ]);

        var storedFields = await Database.HashLengthAsync(EntriesKey);
        var logicalEntries = Math.Max(0, storedFields - 1);

        return new CacheMetricsSnapshot(
            ParseLong(values[0]),
            ParseLong(values[1]),
            logicalEntries,
            RefreshFailures: ParseLong(refreshValues[0]),
            LastRefreshSuccessUnixSeconds: ParseLong(
                refreshValues[1]),
            LastRefreshDurationSeconds: ParseDouble(
                refreshValues[2]),
            LastRefreshLoadedEntries: ParseLong(
                refreshValues[3]));
    }

    private static long ParseLong(RedisValue value) =>
        long.TryParse(
            value.ToString(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;

    private static double ParseDouble(RedisValue value) =>
        double.TryParse(
            value.ToString(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;
}
