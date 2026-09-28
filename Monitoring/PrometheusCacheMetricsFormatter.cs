namespace Monitoring;

public static class PrometheusCacheMetricsFormatter
{
    public static string Format(
        string cacheName,
        CacheMetricsSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheName);

        return FormattableString.Invariant(
            $$"""
            # HELP happy_headlines_cache_hits_total Cache lookups served from Redis.
            # TYPE happy_headlines_cache_hits_total counter
            happy_headlines_cache_hits_total{cache="{{cacheName}}"} {{snapshot.Hits}}
            # HELP happy_headlines_cache_misses_total Cache lookups not found in Redis.
            # TYPE happy_headlines_cache_misses_total counter
            happy_headlines_cache_misses_total{cache="{{cacheName}}"} {{snapshot.Misses}}
            # HELP happy_headlines_cache_entries Current logical cache entry count.
            # TYPE happy_headlines_cache_entries gauge
            happy_headlines_cache_entries{cache="{{cacheName}}"} {{snapshot.Entries}}

            """);
    }
}
