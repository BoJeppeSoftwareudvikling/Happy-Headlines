using System.Threading;

namespace Monitoring;

public sealed class CacheOperationMetrics
{
    private long _invalidationFailures;

    public void RecordInvalidationFailure() =>
        Interlocked.Increment(ref _invalidationFailures);

    public CacheOperationMetricsSnapshot GetSnapshot() =>
        new(Interlocked.Read(ref _invalidationFailures));
}

public sealed record CacheOperationMetricsSnapshot(
    long InvalidationFailures);
