using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Messaging;

public static class RabbitMqTraceContext
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public static void Inject(IDictionary<string, object?> headers)
    {
        var context = new PropagationContext(
            Activity.Current?.Context ?? default,
            Baggage.Current);

        Propagator.Inject(context, headers, static (carrier, key, value) =>
        {
            carrier[key] = Encoding.UTF8.GetBytes(value);
        });
    }

    public static PropagationContext Extract(IDictionary<string, object?>? headers)
    {
        return Propagator.Extract(default, headers, static (carrier, key) =>
        {
            if (carrier is null || !carrier.TryGetValue(key, out var value) || value is null)
            {
                return [];
            }

            return value switch
            {
                byte[] bytes => [Encoding.UTF8.GetString(bytes)],
                ReadOnlyMemory<byte> bytes => [Encoding.UTF8.GetString(bytes.Span)],
                _ => [value.ToString() ?? string.Empty]
            };
        });
    }
}
