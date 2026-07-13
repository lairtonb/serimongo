using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SeriMongo.Services
{
    public sealed class OtlpProtobufLogMapper
    {
        public IReadOnlyList<LogEntry> Map(ExportLogsServiceRequest request)
        {
            var logEntries = new List<LogEntry>();

            foreach (var resourceLogs in request.ResourceLogs)
            {
                var resourceProperties = new Dictionary<string, object>();
                if (resourceLogs.Resource != null)
                {
                    ReadAttributes(resourceProperties, resourceLogs.Resource.Attributes, "resource.");
                }

                foreach (var scopeLogs in resourceLogs.ScopeLogs)
                {
                    var scopeProperties = ReadScopeProperties(scopeLogs);

                    foreach (var logRecord in scopeLogs.LogRecords)
                    {
                        var properties = new Dictionary<string, object>();
                        Merge(properties, resourceProperties);
                        Merge(properties, scopeProperties);
                        ReadAttributes(properties, logRecord.Attributes, string.Empty);
                        AddTraceContext(properties, logRecord);

                        var body = logRecord.Body != null ? ReadAnyValue(logRecord.Body) : null;

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

        private static Dictionary<string, object> ReadScopeProperties(ScopeLogs scopeLogs)
        {
            var properties = new Dictionary<string, object>();
            if (scopeLogs.Scope == null)
            {
                return properties;
            }

            ReadAttributes(properties, scopeLogs.Scope.Attributes, "scope.");

            if (!string.IsNullOrEmpty(scopeLogs.Scope.Name))
            {
                properties["scope.name"] = scopeLogs.Scope.Name;
            }

            if (!string.IsNullOrEmpty(scopeLogs.Scope.Version))
            {
                properties["scope.version"] = scopeLogs.Scope.Version;
            }

            return properties;
        }

        private static void ReadAttributes(
            Dictionary<string, object> target,
            IEnumerable<KeyValue> attributes,
            string prefix)
        {
            foreach (var attribute in attributes)
            {
                if (string.IsNullOrWhiteSpace(attribute.Key) || attribute.Value == null)
                {
                    continue;
                }

                target[prefix + attribute.Key] = ReadAnyValue(attribute.Value);
            }
        }

        private static object ReadAnyValue(AnyValue value)
        {
            switch (value.ValueCase)
            {
                case AnyValue.ValueOneofCase.StringValue:
                    return value.StringValue;
                case AnyValue.ValueOneofCase.BoolValue:
                    return value.BoolValue;
                case AnyValue.ValueOneofCase.IntValue:
                    return value.IntValue;
                case AnyValue.ValueOneofCase.DoubleValue:
                    return value.DoubleValue;
                case AnyValue.ValueOneofCase.BytesValue:
                    return Convert.ToBase64String(value.BytesValue.ToByteArray());
                case AnyValue.ValueOneofCase.ArrayValue:
                    return value.ArrayValue.Values.Select(ReadAnyValue).ToArray();
                case AnyValue.ValueOneofCase.KvlistValue:
                    return ReadKeyValueList(value.KvlistValue);
                default:
                    return null;
            }
        }

        private static Dictionary<string, object> ReadKeyValueList(KeyValueList keyValueList)
        {
            var properties = new Dictionary<string, object>();
            foreach (var item in keyValueList.Values)
            {
                if (!string.IsNullOrWhiteSpace(item.Key) && item.Value != null)
                {
                    properties[item.Key] = ReadAnyValue(item.Value);
                }
            }

            return properties;
        }

        private static DateTimeOffset ReadTimestamp(LogRecord logRecord)
        {
            var unixNano = logRecord.TimeUnixNano != 0
                ? logRecord.TimeUnixNano
                : logRecord.ObservedTimeUnixNano;

            if (unixNano == 0)
            {
                return DateTimeOffset.UtcNow;
            }

            var seconds = (long)(unixNano / 1_000_000_000UL);
            var nanoseconds = (long)(unixNano % 1_000_000_000UL);
            return DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(nanoseconds / 100L);
        }

        private static string ReadSeverity(LogRecord logRecord)
        {
            if (!string.IsNullOrWhiteSpace(logRecord.SeverityText))
            {
                return OtlpSeverity.NormalizeText(logRecord.SeverityText);
            }

            return OtlpSeverity.FromNumber((int)logRecord.SeverityNumber);
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

        private static void AddTraceContext(Dictionary<string, object> properties, LogRecord logRecord)
        {
            if (!logRecord.TraceId.IsEmpty)
            {
                properties["traceId"] = Convert.ToHexStringLower(logRecord.TraceId.ToByteArray());
            }

            if (!logRecord.SpanId.IsEmpty)
            {
                properties["spanId"] = Convert.ToHexStringLower(logRecord.SpanId.ToByteArray());
            }

            if (logRecord.SeverityNumber != SeverityNumber.Unspecified)
            {
                properties["severityNumber"] = (long)logRecord.SeverityNumber;
            }
        }

        private static void Merge(
            Dictionary<string, object> target,
            Dictionary<string, object> source)
        {
            foreach (var item in source)
            {
                target[item.Key] = item.Value;
            }
        }
    }
}
