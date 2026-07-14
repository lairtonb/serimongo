using System;
using System.Collections.Generic;

namespace SeriMongo.Models
{
    public sealed class TraceSearchPage
    {
        public IReadOnlyList<TraceSummary> Items { get; set; } = Array.Empty<TraceSummary>();

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}
