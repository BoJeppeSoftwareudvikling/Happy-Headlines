namespace Monitoring;

public sealed record CacheMetricsSnapshot(
    long Hits,
    long Misses,
    long Entries);
