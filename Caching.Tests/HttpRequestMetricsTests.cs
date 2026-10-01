using Monitoring;
using Xunit;

namespace Caching.Tests;

public sealed class HttpRequestMetricsTests
{
    [Fact]
    public void Observe_records_request_errors_and_cumulative_buckets()
    {
        var metrics = new HttpRequestMetrics();

        metrics.Observe(
            TimeSpan.FromMilliseconds(20),
            statusCode: 200);
        metrics.Observe(
            TimeSpan.FromSeconds(2),
            statusCode: 503);

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(2L, snapshot.Requests);
        Assert.Equal(1L, snapshot.Errors);
        Assert.Equal(0L, snapshot.DurationBucketCounts[0]);
        Assert.Equal(1L, snapshot.DurationBucketCounts[1]);
        Assert.Equal(2L, snapshot.DurationBucketCounts[7]);
        Assert.Equal(2.02, snapshot.DurationSumSeconds, precision: 3);
    }

    [Fact]
    public void Formatter_exposes_service_impact_and_cache_dependency_metrics()
    {
        var requestMetrics = new HttpRequestMetrics();
        requestMetrics.Observe(
            TimeSpan.FromMilliseconds(50),
            statusCode: 200);

        var output = PrometheusCacheMetricsFormatter.Format(
            "article",
            "ArticleService",
            new CacheMetricsSnapshot(3, 1, 2),
            new CacheOperationMetrics().GetSnapshot(),
            requestMetrics.GetSnapshot());

        Assert.Contains(
            "happy_headlines_service_requests_total{service=\"ArticleService\"} 1",
            output);
        Assert.Contains(
            "happy_headlines_cache_dependency_up{cache=\"article\"} 1",
            output);
        Assert.Contains(
            "happy_headlines_cache_hits_total{cache=\"article\"} 3",
            output);
    }

    [Fact]
    public void Formatter_keeps_service_metrics_when_cache_is_unavailable()
    {
        var operationMetrics = new CacheOperationMetrics();
        operationMetrics.RecordInvalidationFailure();

        var output = PrometheusCacheMetricsFormatter.Format(
            "comment",
            "CommentService",
            cacheSnapshot: null,
            operationMetrics.GetSnapshot(),
            new HttpRequestMetrics().GetSnapshot());

        Assert.Contains(
            "happy_headlines_cache_dependency_up{cache=\"comment\"} 0",
            output);
        Assert.DoesNotContain(
            "happy_headlines_cache_hits_total{cache=\"comment\"}",
            output);
        Assert.Contains(
            "happy_headlines_cache_invalidation_failures_total{cache=\"comment\"} 1",
            output);
    }
}
