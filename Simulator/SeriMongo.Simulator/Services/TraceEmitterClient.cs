using System.Globalization;
using System.Net.Http.Json;

namespace SeriMongo.Simulator.Services;

public sealed class TraceEmitterClient(HttpClient httpClient)
{
    public async Task EmitAsync(IReadOnlyCollection<SimulatedSpan> spans, CancellationToken cancellationToken)
    {
        if (spans.Count == 0)
        {
            return;
        }

        var payload = new
        {
            resourceSpans = spans
                .GroupBy(span => span.ServiceName, StringComparer.OrdinalIgnoreCase)
                .Select(BuildResourceSpans)
                .ToArray()
        };

        using var response = await httpClient.PostAsJsonAsync("v1/traces", payload, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Target returned HTTP {(int)response.StatusCode}: {content}");
    }

    private static object BuildResourceSpans(IGrouping<string, SimulatedSpan> spans)
    {
        return new
        {
            resource = new
            {
                attributes = BuildAttributes(new Dictionary<string, object?>
                {
                    ["service.name"] = spans.Key,
                    ["service.namespace"] = "SeriMongo.Simulator",
                    ["deployment.environment"] = "local"
                })
            },
            scopeSpans = new[]
            {
                new
                {
                    scope = new
                    {
                        name = "SeriMongo.Simulator",
                        version = "1.0.0"
                    },
                    spans = spans.Select(BuildSpan).ToArray()
                }
            }
        };
    }

    private static object BuildSpan(SimulatedSpan span)
    {
        return new
        {
            traceId = span.TraceId,
            spanId = span.SpanId,
            parentSpanId = span.ParentSpanId,
            flags = 1,
            name = span.Name,
            kind = span.Kind,
            startTimeUnixNano = ToUnixNano(span.StartTimestamp),
            endTimeUnixNano = ToUnixNano(span.EndTimestamp),
            attributes = BuildAttributes(span.Attributes),
            status = new
            {
                code = span.StatusCode,
                message = span.StatusMessage ?? string.Empty
            }
        };
    }

    private static object[] BuildAttributes(IReadOnlyDictionary<string, object?> attributes)
    {
        return attributes.Select(attribute => new
        {
            key = attribute.Key,
            value = ToAnyValue(attribute.Value)
        }).ToArray<object>();
    }

    private static object ToAnyValue(object? value)
    {
        return value switch
        {
            null => new { stringValue = string.Empty },
            string text => new { stringValue = text },
            bool boolean => new { boolValue = boolean },
            int integer => new { intValue = integer.ToString(CultureInfo.InvariantCulture) },
            long integer => new { intValue = integer.ToString(CultureInfo.InvariantCulture) },
            double number => new { doubleValue = number },
            float number => new { doubleValue = Convert.ToDouble(number, CultureInfo.InvariantCulture) },
            decimal number => new { doubleValue = Convert.ToDouble(number, CultureInfo.InvariantCulture) },
            _ => new { stringValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty }
        };
    }

    private static string ToUnixNano(DateTimeOffset timestamp)
    {
        var unixTicks = timestamp.UtcDateTime.Ticks - DateTime.UnixEpoch.Ticks;
        return (unixTicks * 100L).ToString(CultureInfo.InvariantCulture);
    }
}
