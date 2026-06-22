using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace SeriMongo.Services
{
    public class OtlpLogMapper
    {
        public IReadOnlyList<LogEntry> Map(JsonDocument document)
        {
            var logEntries = new List<LogEntry>();
            var root = document.RootElement;

            if (!root.TryGetProperty("resourceLogs", out var resourceLogs) || resourceLogs.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("OTLP payload must contain a resourceLogs array.");
            }

            foreach (var resourceLog in resourceLogs.EnumerateArray())
            {
                var resourceProperties = ReadNestedAttributes(resourceLog, "resource", "resource.");

                if (!resourceLog.TryGetProperty("scopeLogs", out var scopeLogs) || scopeLogs.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var scopeLog in scopeLogs.EnumerateArray())
                {
                    var scopeProperties = ReadScopeProperties(scopeLog);

                    if (!scopeLog.TryGetProperty("logRecords", out var logRecords) || logRecords.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var logRecord in logRecords.EnumerateArray())
                    {
                        var properties = new Dictionary<string, object>();
                        Merge(properties, resourceProperties);
                        Merge(properties, scopeProperties);
                        Merge(properties, ReadAttributes(logRecord, "attributes", string.Empty));
                        AddTraceContext(properties, logRecord);

                        var body = logRecord.TryGetProperty("body", out var bodyValue)
                            ? ReadAnyValue(bodyValue)
                            : null;

                        logEntries.Add(new LogEntry
                        {
                            Timestamp = ReadTimestamp(logRecord),
                            Level = ReadSeverity(logRecord),
                            RenderedMessage = RenderBody(body),
                            Exception = ReadException(properties),
                            Properties = properties
                        });
                    }
                }
            }

            return logEntries;
        }

        private static Dictionary<string, object> ReadScopeProperties(JsonElement scopeLog)
        {
            var properties = ReadNestedAttributes(scopeLog, "scope", "scope.");

            if (scopeLog.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.Object)
            {
                if (scope.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                {
                    properties["scope.name"] = name.GetString();
                }

                if (scope.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String)
                {
                    properties["scope.version"] = version.GetString();
                }
            }

            return properties;
        }

        private static Dictionary<string, object> ReadNestedAttributes(JsonElement owner, string objectName, string prefix)
        {
            if (!owner.TryGetProperty(objectName, out var nested) || nested.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, object>();
            }

            return ReadAttributes(nested, "attributes", prefix);
        }

        private static Dictionary<string, object> ReadAttributes(JsonElement owner, string propertyName, string prefix)
        {
            var properties = new Dictionary<string, object>();
            if (!owner.TryGetProperty(propertyName, out var attributes) || attributes.ValueKind != JsonValueKind.Array)
            {
                return properties;
            }

            foreach (var attribute in attributes.EnumerateArray())
            {
                if (!attribute.TryGetProperty("key", out var keyElement) || keyElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (!attribute.TryGetProperty("value", out var valueElement))
                {
                    continue;
                }

                var key = keyElement.GetString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    properties[prefix + key] = ReadAnyValue(valueElement);
                }
            }

            return properties;
        }

        private static object ReadAnyValue(JsonElement valueElement)
        {
            if (valueElement.TryGetProperty("stringValue", out var stringValue))
            {
                return stringValue.GetString();
            }

            if (valueElement.TryGetProperty("boolValue", out var boolValue))
            {
                return boolValue.GetBoolean();
            }

            if (valueElement.TryGetProperty("intValue", out var intValue))
            {
                return ReadInt64(intValue);
            }

            if (valueElement.TryGetProperty("doubleValue", out var doubleValue))
            {
                return doubleValue.GetDouble();
            }

            if (valueElement.TryGetProperty("bytesValue", out var bytesValue))
            {
                return bytesValue.GetString();
            }

            if (valueElement.TryGetProperty("arrayValue", out var arrayValue)
                && arrayValue.TryGetProperty("values", out var values)
                && values.ValueKind == JsonValueKind.Array)
            {
                return values.EnumerateArray().Select(ReadAnyValue).ToArray();
            }

            if (valueElement.TryGetProperty("kvlistValue", out var kvListValue)
                && kvListValue.TryGetProperty("values", out var kvValues)
                && kvValues.ValueKind == JsonValueKind.Array)
            {
                var properties = new Dictionary<string, object>();
                foreach (var item in kvValues.EnumerateArray())
                {
                    if (item.TryGetProperty("key", out var keyElement)
                        && keyElement.ValueKind == JsonValueKind.String
                        && item.TryGetProperty("value", out var nestedValue))
                    {
                        properties[keyElement.GetString()] = ReadAnyValue(nestedValue);
                    }
                }

                return properties;
            }

            return valueElement.ValueKind switch
            {
                JsonValueKind.String => valueElement.GetString(),
                JsonValueKind.Number => valueElement.TryGetInt64(out var integer) ? integer : valueElement.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => valueElement.GetRawText()
            };
        }

        private static DateTimeOffset ReadTimestamp(JsonElement logRecord)
        {
            if (TryGetUnixNano(logRecord, "timeUnixNano", out var timeUnixNano)
                || TryGetUnixNano(logRecord, "observedTimeUnixNano", out timeUnixNano))
            {
                var seconds = (long)(timeUnixNano / 1_000_000_000UL);
                var nanoseconds = (long)(timeUnixNano % 1_000_000_000UL);
                return DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(nanoseconds / 100L);
            }

            return DateTimeOffset.UtcNow;
        }

        private static bool TryGetUnixNano(JsonElement owner, string propertyName, out ulong value)
        {
            value = 0;
            if (!owner.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetUInt64(out value))
            {
                return value > 0;
            }

            if (property.ValueKind == JsonValueKind.String
                && ulong.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value > 0;
            }

            return false;
        }

        private static string ReadSeverity(JsonElement logRecord)
        {
            if (logRecord.TryGetProperty("severityText", out var severityText)
                && severityText.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(severityText.GetString()))
            {
                return NormalizeSeverityText(severityText.GetString());
            }

            if (logRecord.TryGetProperty("severityNumber", out var severityNumber))
            {
                var number = (int)ReadInt64(severityNumber);
                return number switch
                {
                    >= 21 => "Fatal",
                    >= 17 => "Error",
                    >= 13 => "Warning",
                    >= 9 => "Information",
                    >= 5 => "Debug",
                    _ => "Trace"
                };
            }

            return "Information";
        }

        private static string NormalizeSeverityText(string severity)
        {
            switch ((severity ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "TRACE":
                case "TRACE1":
                case "TRACE2":
                case "TRACE3":
                case "TRACE4":
                    return "Trace";
                case "DEBUG":
                case "DEBUG1":
                case "DEBUG2":
                case "DEBUG3":
                case "DEBUG4":
                    return "Debug";
                case "INFO":
                case "INFORMATION":
                case "INFO1":
                case "INFO2":
                case "INFO3":
                case "INFO4":
                    return "Information";
                case "WARN":
                case "WARNING":
                case "WARN1":
                case "WARN2":
                case "WARN3":
                case "WARN4":
                    return "Warning";
                case "ERROR":
                case "ERROR1":
                case "ERROR2":
                case "ERROR3":
                case "ERROR4":
                    return "Error";
                case "FATAL":
                case "CRITICAL":
                case "FATAL1":
                case "FATAL2":
                case "FATAL3":
                case "FATAL4":
                    return "Fatal";
                default:
                    return severity;
            }
        }

        private static long ReadInt64(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String
                && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }

            return 0;
        }

        private static string RenderBody(object body)
        {
            if (body == null)
            {
                return string.Empty;
            }

            return body is string text
                ? text
                : JsonSerializer.Serialize(body);
        }

        private static string ReadException(Dictionary<string, object> properties)
        {
            if (properties.TryGetValue("exception.stacktrace", out var stackTrace) && stackTrace != null)
            {
                return stackTrace.ToString();
            }

            if (properties.TryGetValue("exception.message", out var message) && message != null)
            {
                return message.ToString();
            }

            return null;
        }

        private static void AddTraceContext(Dictionary<string, object> properties, JsonElement logRecord)
        {
            AddStringProperty(properties, logRecord, "traceId");
            AddStringProperty(properties, logRecord, "spanId");

            if (logRecord.TryGetProperty("severityNumber", out var severityNumber))
            {
                properties["severityNumber"] = ReadInt64(severityNumber);
            }
        }

        private static void AddStringProperty(Dictionary<string, object> properties, JsonElement owner, string propertyName)
        {
            if (owner.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(property.GetString()))
            {
                properties[propertyName] = property.GetString();
            }
        }

        private static void Merge(Dictionary<string, object> target, Dictionary<string, object> source)
        {
            foreach (var item in source)
            {
                target[item.Key] = item.Value;
            }
        }
    }
}
