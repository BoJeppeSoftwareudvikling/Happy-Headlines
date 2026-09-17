using System.Diagnostics;
using System.Runtime.CompilerServices;
using Serilog;

namespace Monitoring;

/// <summary>
/// Reconstructs the Here() and BeginTimedOperation() helpers visible in the
/// teaching material; their original implementation was not included.
/// </summary>
public static class LoggerExtensions
{
    public static ILogger Here(
        this ILogger logger,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        var sourceType = Path.GetFileNameWithoutExtension(sourceFilePath);

        return logger
            .ForContext("SourceContext", $"{sourceType}.{memberName}")
            .ForContext("SourceLineNumber", sourceLineNumber);
    }

    public static IDisposable BeginTimedOperation(
        this ILogger logger,
        string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        return new TimedOperation(logger, operationName);
    }

    private sealed class TimedOperation : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _disposed;

        public TimedOperation(ILogger logger, string operationName)
        {
            _logger = logger;
            _operationName = operationName;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _stopwatch.Stop();
            _logger.Debug(
                "{OperationName} completed in {ElapsedMilliseconds} ms",
                _operationName,
                _stopwatch.Elapsed.TotalMilliseconds);
            _disposed = true;
        }
    }
}
