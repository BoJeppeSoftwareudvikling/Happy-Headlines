using System.Diagnostics;
using System.Reflection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;

namespace Monitoring;

/// <summary>
/// Central logging and tracing configuration based on the W38 teaching example.
/// </summary>
public static class MonitorService
{
    private const string DefaultSeqUrl = "http://localhost:5341";
    private const string DefaultZipkinEndpoint = "http://localhost:9411/api/v2/spans";

    public static readonly string ServiceName =
        Environment.GetEnvironmentVariable("MONITORING_SERVICE_NAME")
        ?? Assembly.GetCallingAssembly().GetName().Name
        ?? "Unknown";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    public static TracerProvider TracerProvider { get; }

    public static ILogger Log => Serilog.Log.Logger;

    static MonitorService()
    {
        var seqUrl =
            Environment.GetEnvironmentVariable("MONITORING_SEQ_URL")
            ?? DefaultSeqUrl;
        var zipkinEndpoint =
            Environment.GetEnvironmentVariable("MONITORING_ZIPKIN_ENDPOINT")
            ?? DefaultZipkinEndpoint;

        TracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ActivitySource.Name)
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault().AddService(ServiceName))
            .AddConsoleExporter()
            .AddZipkinExporter(options =>
            {
                options.Endpoint = new Uri(zipkinEndpoint);
            })
            .Build();

        Serilog.Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithSpan()
            .WriteTo.Seq(seqUrl, batchPostingLimit: 10)
            .WriteTo.Console()
            .CreateLogger();
    }

    /// <summary>
    /// Flushes buffered telemetry and releases monitoring resources.
    /// Call this during application shutdown.
    /// </summary>
    public static void Shutdown()
    {
        TracerProvider.ForceFlush();
        TracerProvider.Dispose();
        ActivitySource.Dispose();
        Serilog.Log.CloseAndFlush();
    }
}
