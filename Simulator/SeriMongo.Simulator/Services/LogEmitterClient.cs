using System.Globalization;
using System.Net.Http.Json;

namespace SeriMongo.Simulator.Services;

public sealed class LogEmitterClient(HttpClient httpClient)
{
    public async Task EmitAsync(IReadOnlyCollection<SimulatedLog> logs, CancellationToken cancellationToken)
    {
        if (logs.Count == 0)
        {
            return;
        }

        var payload = BuildPayload(logs);
        using var response = await httpClient.PostAsJsonAsync("v1/logs", payload, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Target returned HTTP {(int)response.StatusCode}: {content}");
    }

    private static object BuildPayload(IEnumerable<SimulatedLog> logs)
    {
        return new
        {
            resourceLogs = logs
                .GroupBy(log => log.ServiceName, StringComparer.OrdinalIgnoreCase)
                .Select(BuildResourceLog)
                .ToArray()
        };
    }

    private static object BuildResourceLog(IGrouping<string, SimulatedLog> logs)
    {
        return new
        {
            resource = new
            {
                attributes = BuildAttributes(new Dictionary<string, object?>
                {
                    ["service.name"] = logs.Key,
                    ["service.namespace"] = "SeriMongo.Simulator",
                    ["deployment.environment"] = "local"
                })
            },
            scopeLogs = new[]
            {
                new
                {
                    scope = new
                    {
                        name = "SeriMongo.Simulator",
                        version = "1.0.0"
                    },
                    logRecords = logs.Select(BuildLogRecord).ToArray()
                }
            }
        };
    }

    private static object BuildLogRecord(SimulatedLog log)
    {
        var now = DateTimeOffset.UtcNow;
        return new
        {
            timeUnixNano = ToUnixNano(now),
            observedTimeUnixNano = ToUnixNano(now),
            severityText = log.Level,
            severityNumber = log.SeverityNumber,
            traceId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            spanId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..16],
            body = ToAnyValue(log.Message),
            attributes = BuildAttributes(log.Properties)
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
