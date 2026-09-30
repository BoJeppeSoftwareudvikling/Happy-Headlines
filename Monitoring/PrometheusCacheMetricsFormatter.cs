using System.Globalization;
using System.Text;

namespace Monitoring;

public static class PrometheusCacheMetricsFormatter
{
    public static string Format(
        string cacheName,
        string serviceName,
        CacheMetricsSnapshot? cacheSnapshot,
        CacheOperationMetricsSnapshot operationSnapshot,
        HttpRequestMetricsSnapshot requestSnapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var output = new StringBuilder();

        output.AppendLine(
            "# HELP happy_headlines_service_requests_total HTTP requests handled by the service, excluding metrics scrapes.");
        output.AppendLine(
            "# TYPE happy_headlines_service_requests_total counter");
        output.AppendLine(FormattableString.Invariant(
            $"happy_headlines_service_requests_total{{service=\"{serviceName}\"}} {requestSnapshot.Requests}"));
        output.AppendLine(
            "# HELP happy_headlines_service_errors_total HTTP requests that ended with a 5xx response or unhandled exception.");
        output.AppendLine(
            "# TYPE happy_headlines_service_errors_total counter");
        output.AppendLine(FormattableString.Invariant(
            $"happy_headlines_service_errors_total{{service=\"{serviceName}\"}} {requestSnapshot.Errors}"));
        output.AppendLine(
            "# HELP happy_headlines_service_request_duration_seconds Service HTTP request duration.");
        output.AppendLine(
            "# TYPE happy_headlines_service_request_duration_seconds histogram");

        for (var index = 0;
             index < HttpRequestMetrics.DurationBuckets.Length;
             index++)
        {
            var upperBound = HttpRequestMetrics.DurationBuckets[index]
                .ToString(CultureInfo.InvariantCulture);

            output.AppendLine(FormattableString.Invariant(
                $"happy_headlines_service_request_duration_seconds_bucket{{service=\"{serviceName}\",le=\"{upperBound}\"}} {requestSnapshot.DurationBucketCounts[index]}"));
        }

        output.AppendLine(FormattableString.Invariant(
            $"happy_headlines_service_request_duration_seconds_bucket{{service=\"{serviceName}\",le=\"+Inf\"}} {requestSnapshot.Requests}"));
        output.AppendLine(FormattableString.Invariant(
            $"happy_headlines_service_request_duration_seconds_sum{{service=\"{serviceName}\"}} {requestSnapshot.DurationSumSeconds}"));
        output.AppendLine(FormattableString.Invariant(
            $"happy_headlines_service_request_duration_seconds_count{{service=\"{serviceName}\"}} {requestSnapshot.Requests}"));
        output.AppendLine(
            "# HELP happy_headlines_cache_dependency_up Whether the service could read metrics from its Redis cache.");
        output.AppendLine(
            "# TYPE happy_headlines_cache_dependency_up gauge");
        output.AppendLine(FormattableString.Invariant(
            $"happy_headlines_cache_dependency_up{{cache=\"{cacheName}\"}} {(cacheSnapshot is null ? 0 : 1)}"));
        output.AppendLine(
            "# HELP happy_headlines_cache_invalidation_failures_total Cache invalidation attempts that failed before the cached entry could be removed.");
        output.AppendLine(
            "# TYPE happy_headlines_cache_invalidation_failures_total counter");
        output.AppendLine(FormattableString.Invariant(
            $"happy_headlines_cache_invalidation_failures_total{{cache=\"{cacheName}\"}} {operationSnapshot.InvalidationFailures}"));

        if (cacheSnapshot is null)
        {
            return output.ToString();
        }

        output.Append(FormattableString.Invariant(
            $$"""
            # HELP happy_headlines_cache_hits_total Cache lookups served from Redis.
            # TYPE happy_headlines_cache_hits_total counter
            happy_headlines_cache_hits_total{cache="{{cacheName}}"} {{cacheSnapshot.Hits}}
            # HELP happy_headlines_cache_misses_total Cache lookups not found in Redis.
            # TYPE happy_headlines_cache_misses_total counter
            happy_headlines_cache_misses_total{cache="{{cacheName}}"} {{cacheSnapshot.Misses}}
            # HELP happy_headlines_cache_entries Current logical cache entry count.
            # TYPE happy_headlines_cache_entries gauge
            happy_headlines_cache_entries{cache="{{cacheName}}"} {{cacheSnapshot.Entries}}
            # HELP happy_headlines_cache_evictions_total Entries removed automatically because a cache reached capacity.
            # TYPE happy_headlines_cache_evictions_total counter
            happy_headlines_cache_evictions_total{cache="{{cacheName}}"} {{cacheSnapshot.Evictions}}
            # HELP happy_headlines_cache_invalidations_total Cached entries removed after application writes.
            # TYPE happy_headlines_cache_invalidations_total counter
            happy_headlines_cache_invalidations_total{cache="{{cacheName}}"} {{cacheSnapshot.Invalidations}}
            # HELP happy_headlines_cache_refresh_failures_total ArticleCache refresh attempts that failed.
            # TYPE happy_headlines_cache_refresh_failures_total counter
            happy_headlines_cache_refresh_failures_total{cache="{{cacheName}}"} {{cacheSnapshot.RefreshFailures}}
            # HELP happy_headlines_cache_last_refresh_success_timestamp_seconds Unix timestamp of the last successful ArticleCache refresh.
            # TYPE happy_headlines_cache_last_refresh_success_timestamp_seconds gauge
            happy_headlines_cache_last_refresh_success_timestamp_seconds{cache="{{cacheName}}"} {{cacheSnapshot.LastRefreshSuccessUnixSeconds}}
            # HELP happy_headlines_cache_last_refresh_duration_seconds Duration of the last successful ArticleCache refresh.
            # TYPE happy_headlines_cache_last_refresh_duration_seconds gauge
            happy_headlines_cache_last_refresh_duration_seconds{cache="{{cacheName}}"} {{cacheSnapshot.LastRefreshDurationSeconds}}
            # HELP happy_headlines_cache_last_refresh_loaded_entries Number of articles loaded by the last successful ArticleCache refresh.
            # TYPE happy_headlines_cache_last_refresh_loaded_entries gauge
            happy_headlines_cache_last_refresh_loaded_entries{cache="{{cacheName}}"} {{cacheSnapshot.LastRefreshLoadedEntries}}

            """));

        return output.ToString();
    }
}
