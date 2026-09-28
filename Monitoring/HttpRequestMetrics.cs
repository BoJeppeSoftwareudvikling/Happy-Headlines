using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Monitoring;

public sealed class HttpRequestMetrics
{
    public static readonly double[] DurationBuckets =
    [
        0.01,
        0.025,
        0.05,
        0.1,
        0.25,
        0.5,
        1,
        2.5,
        5
    ];

    private readonly object gate = new();
    private readonly long[] durationBucketCounts =
        new long[DurationBuckets.Length];

    private long requests;
    private long errors;
    private double durationSumSeconds;

    public void Observe(TimeSpan duration, int statusCode)
    {
        var durationSeconds = duration.TotalSeconds;

        lock (gate)
        {
            requests++;
            durationSumSeconds += durationSeconds;

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                errors++;
            }

            for (var index = 0;
                 index < DurationBuckets.Length;
                 index++)
            {
                if (durationSeconds <= DurationBuckets[index])
                {
                    durationBucketCounts[index]++;
                }
            }
        }
    }

    public HttpRequestMetricsSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new HttpRequestMetricsSnapshot(
                requests,
                errors,
                durationSumSeconds,
                [.. durationBucketCounts]);
        }
    }
}

public sealed record HttpRequestMetricsSnapshot(
    long Requests,
    long Errors,
    double DurationSumSeconds,
    IReadOnlyList<long> DurationBucketCounts);

public static class HttpRequestMetricsApplicationBuilderExtensions
{
    public static IApplicationBuilder UseHappyHeadlinesRequestMetrics(
        this IApplicationBuilder app)
    {
        return app.Use(
            async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments(
                        "/metrics"))
                {
                    await next(context);
                    return;
                }

                var metrics = context.RequestServices
                    .GetRequiredService<HttpRequestMetrics>();
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    await next(context);
                    metrics.Observe(
                        stopwatch.Elapsed,
                        context.Response.StatusCode);
                }
                catch
                {
                    metrics.Observe(
                        stopwatch.Elapsed,
                        StatusCodes.Status500InternalServerError);
                    throw;
                }
            });
    }
}
