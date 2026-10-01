namespace Monitoring;

public sealed record CacheMetricsSnapshot(
    long Hits,
    long Misses,
    long Entries,
    long Evictions = 0,
    long Invalidations = 0,
    long RefreshFailures = 0,
    long LastRefreshSuccessUnixSeconds = 0,
    double LastRefreshDurationSeconds = 0,
    long LastRefreshLoadedEntries = 0);
