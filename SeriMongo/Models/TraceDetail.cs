using System;
using System.Collections.Generic;

namespace SeriMongo.Models
{
    public sealed class TraceDetail
    {
        public TraceSummary Summary { get; set; } = new TraceSummary();

        public IReadOnlyList<TraceSpan> Spans { get; set; } = Array.Empty<TraceSpan>();
    }
}
