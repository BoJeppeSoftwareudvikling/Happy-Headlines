using ArticleService.Caching;
using ArticleService.Models;
using CommentService.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace Caching.Tests;

[CollectionDefinition(
    RedisCollection.Name,
    DisableParallelization = true)]
public sealed class RedisCollection :
    ICollectionFixture<RedisFixture>
{
    public const string Name = "Redis caches";
}

public sealed class RedisFixture : IAsyncLifetime
{
    public IConnectionMultiplexer ArticleRedis { get; private set; } =
        null!;

    public IConnectionMultiplexer CommentRedis { get; private set; } =
        null!;

    public async Task InitializeAsync()
    {
        ArticleRedis = await ConnectionMultiplexer.ConnectAsync(
            "localhost:6380,allowAdmin=true,abortConnect=false");

        CommentRedis = await ConnectionMultiplexer.ConnectAsync(
            "localhost:6381,allowAdmin=true,abortConnect=false");
    }

    public async Task ResetArticleAsync()
    {
        var endpoint = ArticleRedis.GetEndPoints().Single();
        await ArticleRedis.GetServer(endpoint).FlushDatabaseAsync();
    }

    public async Task ResetCommentAsync()
    {
        var endpoint = CommentRedis.GetEndPoints().Single();
        await CommentRedis.GetServer(endpoint).FlushDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await ArticleRedis.CloseAsync();
        await CommentRedis.CloseAsync();

        ArticleRedis.Dispose();
        CommentRedis.Dispose();
    }
}

[Collection(RedisCollection.Name)]
public sealed class RedisCacheTests(RedisFixture fixture)
{
    [Fact]
    public async Task Article_cache_replacement_removes_old_entries()
    {
        await fixture.ResetArticleAsync();

        var cache = new RedisArticleCache(
            fixture.ArticleRedis,
            NullLogger<RedisArticleCache>.Instance);

        var first = new Article
        {
            Id = 1,
            Title = "First",
            Content = "First content",
            Continent = Continent.Global,
            PublishedAtUtc = DateTimeOffset.UtcNow
        };

        var second = new Article
        {
            Id = 2,
            Title = "Second",
            Content = "Second content",
            Continent = Continent.Global,
            PublishedAtUtc = DateTimeOffset.UtcNow
        };

        await cache.ReplaceAsync([first, second]);

        Assert.NotNull(await cache.GetAsync(1));
        Assert.NotNull(await cache.GetAsync(2));

        await cache.ReplaceAsync([second]);

        Assert.Null(await cache.GetAsync(1));
        Assert.NotNull(await cache.GetAsync(2));

        var metrics = await cache.GetMetricsAsync();
        Assert.Equal(1L, metrics.Entries);
    }

    [Fact]
    public async Task Article_cache_records_refresh_diagnostics()
    {
        await fixture.ResetArticleAsync();

        var cache = new RedisArticleCache(
            fixture.ArticleRedis,
            NullLogger<RedisArticleCache>.Instance);

        var completedAt = DateTimeOffset.FromUnixTimeSeconds(
            1_800_000_000);

        await cache.RecordRefreshSuccessAsync(
            completedAt,
            TimeSpan.FromMilliseconds(250),
            12);
        await cache.RecordRefreshFailureAsync();

        var metrics = await cache.GetMetricsAsync();

        Assert.Equal(1L, metrics.RefreshFailures);
        Assert.Equal(
            completedAt.ToUnixTimeSeconds(),
            metrics.LastRefreshSuccessUnixSeconds);
        Assert.Equal(
            0.25,
            metrics.LastRefreshDurationSeconds,
            precision: 3);
        Assert.Equal(12L, metrics.LastRefreshLoadedEntries);
    }

    [Fact]
    public async Task Comment_cache_populates_on_miss_then_hits()
    {
        await fixture.ResetCommentAsync();

        var cache = new RedisCommentCache(
            fixture.CommentRedis,
            NullLogger<RedisCommentCache>.Instance);

        Assert.Null(await cache.GetAsync(42));

        await cache.SetAsync(
            42,
            [
                new CommentService.Models.Comment
                {
                    Id = 1,
                    ArticleId = 42,
                    AuthorName = "Reader",
                    Content = "Good news",
                    CreatedAtUtc = DateTime.UtcNow
                }
            ]);

        var cached = await cache.GetAsync(42);

        Assert.NotNull(cached);
        Assert.Single(cached);

        var metrics = await cache.GetMetricsAsync();

        Assert.Equal(1L, metrics.Hits);
        Assert.Equal(1L, metrics.Misses);
    }

    [Fact]
    public async Task Comment_cache_evicts_the_least_recently_used_entry()
    {
        await fixture.ResetCommentAsync();

        var cache = new RedisCommentCache(
            fixture.CommentRedis,
            NullLogger<RedisCommentCache>.Instance);

        for (var articleId = 1; articleId <= 30; articleId++)
        {
            await cache.SetAsync(articleId, []);
        }

        Assert.NotNull(await cache.GetAsync(1));

        await cache.SetAsync(31, []);

        Assert.NotNull(await cache.GetAsync(1));
        Assert.Null(await cache.GetAsync(2));
        Assert.NotNull(await cache.GetAsync(31));

        var metrics = await cache.GetMetricsAsync();
        Assert.Equal(30L, metrics.Entries);
        Assert.Equal(1L, metrics.Evictions);
    }

    [Fact]
    public async Task Comment_cache_invalidation_removes_the_article_list()
    {
        await fixture.ResetCommentAsync();

        var cache = new RedisCommentCache(
            fixture.CommentRedis,
            NullLogger<RedisCommentCache>.Instance);

        await cache.SetAsync(7, []);
        Assert.NotNull(await cache.GetAsync(7));

        await cache.RemoveAsync(7);

        Assert.Null(await cache.GetAsync(7));

        var metrics = await cache.GetMetricsAsync();
        Assert.Equal(1L, metrics.Invalidations);
    }
}
