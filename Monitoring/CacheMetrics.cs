using Prometheus;

namespace Monitoring;

public static class CacheMetrics
{
    private static readonly Counter Requests = Metrics.CreateCounter(
        "happy_headlines_cache_requests_total",
        "Cache read requests grouped by cache and outcome.",
        new CounterConfiguration
        {
            LabelNames = ["cache", "outcome"]
        });

    private static readonly Counter Invalidations = Metrics.CreateCounter(
        "happy_headlines_cache_invalidations_total",
        "Cache invalidations grouped by cache and outcome.",
        new CounterConfiguration
        {
            LabelNames = ["cache", "outcome"]
        });

    private static readonly Counter Fallbacks = Metrics.CreateCounter(
        "happy_headlines_cache_fallbacks_total",
        "Database fallbacks grouped by cache and reason.",
        new CounterConfiguration
        {
            LabelNames = ["cache", "reason"]
        });

    private static readonly Counter CommentEvictions = Metrics.CreateCounter(
        "happy_headlines_comment_cache_evictions_total",
        "Comment article entries evicted by the LRU policy.");

    public static void RecordHit(string cacheName) =>
        Requests.WithLabels(cacheName, "hit").Inc();

    public static void RecordMiss(string cacheName) =>
        Requests.WithLabels(cacheName, "miss").Inc();

    public static void RecordError(string cacheName) =>
        Requests.WithLabels(cacheName, "error").Inc();

    public static void RecordInvalidation(
        string cacheName,
        string outcome) =>
        Invalidations.WithLabels(cacheName, outcome).Inc();

    public static void RecordFallback(
        string cacheName,
        string reason) =>
        Fallbacks.WithLabels(cacheName, reason).Inc();

    public static void RecordCommentEviction(int count)
    {
        if (count > 0)
        {
            CommentEvictions.Inc(count);
        }
    }
}
