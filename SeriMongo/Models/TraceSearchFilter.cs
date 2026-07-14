using System;

namespace SeriMongo.Models
{
    public sealed class TraceSearchFilter
    {
        public DateTimeOffset? StartTimestamp { get; set; }

        public DateTimeOffset? EndTimestamp { get; set; }

        public string TraceId { get; set; }

        public string ServiceName { get; set; }

        public string RootOperationName { get; set; }

        public TraceStatusFilter Status { get; set; }

        public long? MinimumDurationNanoseconds { get; set; }

        public long? MaximumDurationNanoseconds { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 100;
    }
}
