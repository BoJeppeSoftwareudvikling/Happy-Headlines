using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ArticleCache.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ArticleCacheWorker;

public sealed class ArticleSnapshotWriter(
    IOptions<GlobalArticleDatabaseOptions> databaseOptions,
    IOptions<ArticleCacheOptions> cacheOptions,
    IConnectionMultiplexer connection,
    ILogger<ArticleSnapshotWriter> logger)
{
    public async Task<int> RefreshAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-14);
        var articles = await ReadRecentArticlesAsync(cutoff, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var database = connection.GetDatabase();
        var version =
            $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";

        var itemsKey = ArticleCacheKeys.SnapshotItems(version);
        var publishedKey = ArticleCacheKeys.SnapshotPublished(version);
        var ttl = TimeSpan.FromSeconds(cacheOptions.Value.SnapshotTtlSeconds);

        if (articles.Count > 0)
        {
            var transaction = database.CreateTransaction();
            var queuedCommands = new List<Task>();

            foreach (var article in articles)
            {
                var articleId = article.Id.ToString(
                    CultureInfo.InvariantCulture);
                var json = JsonSerializer.Serialize(article);

                queuedCommands.Add(transaction.HashSetAsync(
                    itemsKey,
                    articleId,
                    json));

                queuedCommands.Add(transaction.SortedSetAddAsync(
                    publishedKey,
                    articleId,
                    article.PublishedAtUtc.ToUnixTimeMilliseconds()));
            }

            queuedCommands.Add(transaction.KeyExpireAsync(itemsKey, ttl));
            queuedCommands.Add(transaction.KeyExpireAsync(publishedKey, ttl));

            var committed = await transaction.ExecuteAsync();

            if (!committed)
            {
                throw new InvalidOperationException(
                    "Redis did not commit the article snapshot transaction.");
            }

            await Task.WhenAll(queuedCommands);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var storedCount = await database.HashLengthAsync(itemsKey);

        if (storedCount != articles.Count)
        {
            throw new InvalidOperationException(
                $"Snapshot verification failed. Expected {articles.Count} " +
                $"articles but Redis contains {storedCount}.");
        }

        var activated = await database.StringSetAsync(
            ArticleCacheKeys.ActiveVersion,
            version,
            ttl);

        if (!activated)
        {
            throw new InvalidOperationException(
                "Redis did not activate the article snapshot.");
        }

        stopwatch.Stop();

        logger.LogInformation(
            "Activated article snapshot {Version} with {ArticleCount} articles " +
            "using cutoff {Cutoff}. Refresh took {DurationMilliseconds} ms.",
            version,
            articles.Count,
            cutoff,
            stopwatch.ElapsedMilliseconds);

        return articles.Count;
    }

    private async Task<List<CachedArticle>> ReadRecentArticlesAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT [Id], [Title], [Content], [PublishedAtUtc]
            FROM [Articles]
            WHERE [PublishedAtUtc] >= @Cutoff
            ORDER BY [PublishedAtUtc] DESC, [Id] DESC;
            """;

        await using var connection =
            new SqlConnection(databaseOptions.Value.Global);

        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(
            new SqlParameter("@Cutoff", SqlDbType.DateTimeOffset)
            {
                Value = cutoff
            });

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var articles = new List<CachedArticle>();

        while (await reader.ReadAsync(cancellationToken))
        {
            articles.Add(new CachedArticle
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                Continent = "Global",
                PublishedAtUtc = reader.GetFieldValue<DateTimeOffset>(3)
            });
        }

        return articles;
    }
}