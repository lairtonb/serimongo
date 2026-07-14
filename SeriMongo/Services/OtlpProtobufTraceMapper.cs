using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Trace.V1;
using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SeriMongo.Services
{
    public sealed class OtlpProtobufTraceMapper
    {
        public IReadOnlyList<TraceSpan> Map(ExportTraceServiceRequest request)
        {
            var spans = new List<TraceSpan>();

            foreach (var resourceSpans in request.ResourceSpans)
            {
                var resourceAttributes = ReadAttributes(resourceSpans.Resource?.Attributes);
                var serviceName = GetStringValue(resourceAttributes, "service.name") ?? "unknown-service";
                var resourceAttributesJson = JsonSerializer.Serialize(resourceAttributes);

                foreach (var scopeSpans in resourceSpans.ScopeSpans)
                {
                    var scope = scopeSpans.Scope;
                    var scopeName = scope?.Name;
                    var scopeVersion = scope?.Version;

                    foreach (var span in scopeSpans.Spans)
                    {
                        var status = span.Status;
                        spans.Add(new TraceSpan
                        {
                            TraceId = ToHex(span.TraceId),
                            SpanId = ToHex(span.SpanId),
                            ParentSpanId = span.ParentSpanId.IsEmpty ? null : ToHex(span.ParentSpanId),
                            TraceState = span.TraceState,
                            Flags = span.Flags,
                            Name = span.Name,
                            Kind = (int)span.Kind,
                            StartTimestamp = ReadTimestamp(span.StartTimeUnixNano),
                            EndTimestamp = ReadTimestamp(span.EndTimeUnixNano),
                            DurationNanoseconds = CalculateDuration(span.StartTimeUnixNano, span.EndTimeUnixNano),
                            StatusCode = status is null ? 0 : (int)status.Code,
                            StatusMessage = status?.Message,
                            ServiceName = serviceName,
                            ResourceAttributesJson = resourceAttributesJson,
                            ScopeName = scopeName,
                            ScopeVersion = scopeVersion,
                            AttributesJson = JsonSerializer.Serialize(ReadAttributes(span.Attributes)),
                            EventsJson = JsonSerializer.Serialize(span.Events.Select(ReadEvent)),
                            LinksJson = JsonSerializer.Serialize(span.Links.Select(ReadLink)),
                            DroppedAttributesCount = span.DroppedAttributesCount,
                            DroppedEventsCount = span.DroppedEventsCount,
                            DroppedLinksCount = span.DroppedLinksCount
                        });
                    }
                }
            }

            return spans;
        }

        private static Dictionary<string, object> ReadAttributes(IEnumerable<KeyValue> attributes)
        {
            var values = new Dictionary<string, object>();
            if (attributes == null)
            {
                return values;
            }

            foreach (var attribute in attributes)
            {
                if (!string.IsNullOrWhiteSpace(attribute.Key) && attribute.Value != null)
                {
                    values[attribute.Key] = ReadAnyValue(attribute.Value);
                }
            }

            return values;
        }

        private static object ReadAnyValue(AnyValue value)
        {
            return value.ValueCase switch
            {
                AnyValue.ValueOneofCase.StringValue => value.StringValue,
                AnyValue.ValueOneofCase.BoolValue => value.BoolValue,
                AnyValue.ValueOneofCase.IntValue => value.IntValue,
                AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue,
                AnyValue.ValueOneofCase.BytesValue => Convert.ToBase64String(value.BytesValue.ToByteArray()),
                AnyValue.ValueOneofCase.ArrayValue => value.ArrayValue.Values.Select(ReadAnyValue).ToArray(),
                AnyValue.ValueOneofCase.KvlistValue => ReadAttributes(value.KvlistValue.Values),
                _ => null
            };
        }

        private static object ReadEvent(Span.Types.Event traceEvent)
        {
            return new
            {
                name = traceEvent.Name,
                timeUnixNano = traceEvent.TimeUnixNano.ToString(),
                attributes = ReadAttributes(traceEvent.Attributes),
                droppedAttributesCount = traceEvent.DroppedAttributesCount
            };
        }

        private static object ReadLink(Span.Types.Link link)
        {
            return new
            {
                traceId = ToHex(link.TraceId),
                spanId = ToHex(link.SpanId),
                traceState = link.TraceState,
                flags = link.Flags,
                attributes = ReadAttributes(link.Attributes),
                droppedAttributesCount = link.DroppedAttributesCount
            };
        }

        private static string GetStringValue(IReadOnlyDictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out var value) ? value?.ToString() : null;
        }

        private static DateTimeOffset ReadTimestamp(ulong timestamp)
        {
            if (timestamp == 0)
            {
                return DateTimeOffset.UtcNow;
            }

            var seconds = (long)(timestamp / 1_000_000_000UL);
            var nanoseconds = (long)(timestamp % 1_000_000_000UL);
            return DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(nanoseconds / 100L);
        }

        private static long CalculateDuration(ulong startTimestamp, ulong endTimestamp)
        {
            if (endTimestamp < startTimestamp)
            {
                return 0;
            }

            var duration = endTimestamp - startTimestamp;
            return duration > long.MaxValue ? long.MaxValue : (long)duration;
        }

        private static string ToHex(Google.Protobuf.ByteString bytes)
        {
            return bytes.IsEmpty ? string.Empty : Convert.ToHexStringLower(bytes.ToByteArray());
        }
    }
}
