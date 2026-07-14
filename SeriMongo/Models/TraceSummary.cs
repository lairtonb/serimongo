using System;
using System.Collections.Generic;

namespace SeriMongo.Models
{
    public sealed class TraceSummary
    {
        public string TraceId { get; set; } = string.Empty;

        public DateTimeOffset StartTimestamp { get; set; }

        public DateTimeOffset EndTimestamp { get; set; }

        public long DurationNanoseconds { get; set; }

        public string RootSpanId { get; set; }

        public string RootSpanName { get; set; } = string.Empty;

        public string RootServiceName { get; set; } = string.Empty;

        public int SpanCount { get; set; }

        public int ErrorSpanCount { get; set; }

        public IReadOnlyList<string> ServiceNames { get; set; } = Array.Empty<string>();

        public DateTimeOffset LastUpdated { get; set; }
    }
}
