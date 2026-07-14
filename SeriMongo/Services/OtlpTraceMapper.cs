using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace SeriMongo.Services
{
    public sealed class OtlpTraceMapper
    {
        public IReadOnlyList<TraceSpan> Map(JsonDocument document)
        {
            if (!document.RootElement.TryGetProperty("resourceSpans", out var resourceSpans)
                || resourceSpans.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("OTLP payload must contain a resourceSpans array.");
            }

            var spans = new List<TraceSpan>();
            foreach (var resourceSpan in resourceSpans.EnumerateArray())
            {
                var resourceAttributes = ReadNestedAttributes(resourceSpan, "resource");
                var serviceName = GetStringValue(resourceAttributes, "service.name") ?? "unknown-service";
                var resourceAttributesJson = JsonSerializer.Serialize(resourceAttributes);

                if (!resourceSpan.TryGetProperty("scopeSpans", out var scopeSpans)
                    || scopeSpans.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var scopeSpan in scopeSpans.EnumerateArray())
                {
                    var scopeName = ReadString(scopeSpan, "scope", "name");
                    var scopeVersion = ReadString(scopeSpan, "scope", "version");

                    if (!scopeSpan.TryGetProperty("spans", out var traceSpans)
                        || traceSpans.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var span in traceSpans.EnumerateArray())
                    {
                        var startTimestamp = ReadTimestamp(span, "startTimeUnixNano");
                        var endTimestamp = ReadTimestamp(span, "endTimeUnixNano");
                        spans.Add(new TraceSpan
                        {
                            TraceId = ReadString(span, "traceId") ?? string.Empty,
                            SpanId = ReadString(span, "spanId") ?? string.Empty,
                            ParentSpanId = ReadString(span, "parentSpanId"),
                            TraceState = ReadString(span, "traceState") ?? string.Empty,
                            Flags = (uint)ReadInteger(span, "flags"),
                            Name = ReadString(span, "name") ?? string.Empty,
                            Kind = (int)ReadInteger(span, "kind"),
                            StartTimestamp = startTimestamp,
                            EndTimestamp = endTimestamp,
                            DurationNanoseconds = CalculateDuration(startTimestamp, endTimestamp),
                            StatusCode = (int)ReadInteger(span, "status", "code"),
                            StatusMessage = ReadString(span, "status", "message"),
                            ServiceName = serviceName,
                            ResourceAttributesJson = resourceAttributesJson,
                            ScopeName = scopeName,
                            ScopeVersion = scopeVersion,
                            AttributesJson = JsonSerializer.Serialize(ReadAttributes(span, "attributes")),
                            EventsJson = ReadArrayJson(span, "events"),
                            LinksJson = ReadArrayJson(span, "links"),
                            DroppedAttributesCount = (uint)ReadInteger(span, "droppedAttributesCount"),
                            DroppedEventsCount = (uint)ReadInteger(span, "droppedEventsCount"),
                            DroppedLinksCount = (uint)ReadInteger(span, "droppedLinksCount")
                        });
                    }
                }
            }

            return spans;
        }

        private static Dictionary<string, object> ReadNestedAttributes(JsonElement owner, string propertyName)
        {
            return owner.TryGetProperty(propertyName, out var nested) && nested.ValueKind == JsonValueKind.Object
                ? ReadAttributes(nested, "attributes")
                : new Dictionary<string, object>();
        }

        private static Dictionary<string, object> ReadAttributes(JsonElement owner, string propertyName)
        {
            var attributes = new Dictionary<string, object>();
            if (!owner.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
            {
                return attributes;
            }

            foreach (var attribute in values.EnumerateArray())
            {
                if (attribute.TryGetProperty("key", out var key)
                    && key.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(key.GetString())
                    && attribute.TryGetProperty("value", out var value))
                {
                    attributes[key.GetString()] = ReadAnyValue(value);
                }
            }

            return attributes;
        }

        private static object ReadAnyValue(JsonElement value)
        {
            if (value.TryGetProperty("stringValue", out var stringValue))
            {
                return stringValue.GetString();
            }

            if (value.TryGetProperty("boolValue", out var boolValue))
            {
                return boolValue.GetBoolean();
            }

            if (value.TryGetProperty("intValue", out var intValue))
            {
                return ReadInteger(intValue);
            }

            if (value.TryGetProperty("doubleValue", out var doubleValue))
            {
                return doubleValue.GetDouble();
            }

            if (value.TryGetProperty("bytesValue", out var bytesValue))
            {
                return bytesValue.GetString();
            }

            if (value.TryGetProperty("arrayValue", out var arrayValue)
                && arrayValue.TryGetProperty("values", out var values)
                && values.ValueKind == JsonValueKind.Array)
            {
                return values.EnumerateArray().Select(ReadAnyValue).ToArray();
            }

            if (value.TryGetProperty("kvlistValue", out var keyValueList)
                && keyValueList.TryGetProperty("values", out var keyValueValues)
                && keyValueValues.ValueKind == JsonValueKind.Array)
            {
                var result = new Dictionary<string, object>();
                foreach (var item in keyValueValues.EnumerateArray())
                {
                    if (item.TryGetProperty("key", out var key)
                        && key.ValueKind == JsonValueKind.String
                        && item.TryGetProperty("value", out var nestedValue))
                    {
                        result[key.GetString()] = ReadAnyValue(nestedValue);
                    }
                }

                return result;
            }

            return null;
        }

        private static DateTimeOffset ReadTimestamp(JsonElement owner, string propertyName)
        {
            var value = ReadInteger(owner, propertyName);
            if (value <= 0)
            {
                return DateTimeOffset.UtcNow;
            }

            var seconds = value / 1_000_000_000L;
            var nanoseconds = value % 1_000_000_000L;
            return DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(nanoseconds / 100L);
        }

        private static long CalculateDuration(DateTimeOffset startTimestamp, DateTimeOffset endTimestamp)
        {
            if (endTimestamp < startTimestamp)
            {
                return 0;
            }

            var ticks = endTimestamp.UtcTicks - startTimestamp.UtcTicks;
            return ticks > long.MaxValue / 100L ? long.MaxValue : ticks * 100L;
        }

        private static string ReadArrayJson(JsonElement owner, string propertyName)
        {
            return owner.TryGetProperty(propertyName, out var values) && values.ValueKind == JsonValueKind.Array
                ? values.GetRawText()
                : "[]";
        }

        private static string ReadString(JsonElement owner, params string[] path)
        {
            foreach (var segment in path)
            {
                if (!owner.TryGetProperty(segment, out owner) || owner.ValueKind != JsonValueKind.Object && segment != path[^1])
                {
                    return null;
                }
            }

            return owner.ValueKind == JsonValueKind.String ? owner.GetString() : null;
        }

        private static long ReadInteger(JsonElement owner, params string[] path)
        {
            foreach (var segment in path)
            {
                if (!owner.TryGetProperty(segment, out owner))
                {
                    return 0;
                }
            }

            return ReadInteger(owner);
        }

        private static long ReadInteger(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                return number;
            }

            return value.ValueKind == JsonValueKind.String
                && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                ? number
                : 0;
        }

        private static string GetStringValue(IReadOnlyDictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out var value) ? value?.ToString() : null;
        }
    }
}
