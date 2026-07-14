using System;

namespace SeriMongo.Models
{
    public sealed class TraceSpan
    {
        public string TraceId { get; set; } = string.Empty;

        public string SpanId { get; set; } = string.Empty;

        public string ParentSpanId { get; set; }

        public string TraceState { get; set; } = string.Empty;

        public uint Flags { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Kind { get; set; }

        public DateTimeOffset StartTimestamp { get; set; }

        public DateTimeOffset EndTimestamp { get; set; }

        public long DurationNanoseconds { get; set; }

        public int StatusCode { get; set; }

        public string StatusMessage { get; set; }

        public string ServiceName { get; set; } = string.Empty;

        public string ResourceAttributesJson { get; set; } = "{}";

        public string ScopeName { get; set; }

        public string ScopeVersion { get; set; }

        public string AttributesJson { get; set; } = "{}";

        public string EventsJson { get; set; } = "[]";

        public string LinksJson { get; set; } = "[]";

        public uint DroppedAttributesCount { get; set; }

        public uint DroppedEventsCount { get; set; }

        public uint DroppedLinksCount { get; set; }
    }
}
