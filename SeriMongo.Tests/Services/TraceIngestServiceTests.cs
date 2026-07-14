using SeriMongo.Data;
using SeriMongo.Models;
using SeriMongo.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SeriMongo.Tests.Services
{
    public sealed class TraceIngestServiceTests
    {
        [Fact]
        public async Task IngestAsync_PersistsSpansBeforePublishingTraceSummary()
        {
            var repository = new CapturingTraceRepository();
            var notifier = new CapturingTraceNotifier();
            var service = new TraceIngestService(repository, notifier);
            var traceId = "0123456789abcdef0123456789abcdef";
            var span = new TraceSpan
            {
                TraceId = traceId,
                SpanId = "0123456789abcdef",
                Name = "POST /orders",
                ServiceName = "test-api",
                StartTimestamp = DateTimeOffset.UtcNow,
                EndTimestamp = DateTimeOffset.UtcNow.AddMilliseconds(10)
            };

            var result = await service.IngestAsync([span], TestContext.Current.CancellationToken);

            Assert.Equal(1, result.PersistedSpans);
            Assert.Equal(0, result.RejectedSpans);
            Assert.Equal([span], repository.PersistedSpans);
            var notification = Assert.Single(notifier.Notifications);
            Assert.Equal(traceId, notification.TraceId);
        }

        private sealed class CapturingTraceRepository : ITraceRepository
        {
            public IReadOnlyCollection<TraceSpan> PersistedSpans { get; private set; } = Array.Empty<TraceSpan>();

            public Task UpsertAsync(IReadOnlyCollection<TraceSpan> spans, CancellationToken cancellationToken = default)
            {
                PersistedSpans = spans;
                return Task.CompletedTask;
            }

            public Task<TraceSearchPage> SearchAsync(TraceSearchFilter filter, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new TraceSearchPage
                {
                    Items = [new TraceSummary { TraceId = filter.TraceId }]
                });
            }

            public Task<TraceDetail> GetTraceAsync(string traceId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class CapturingTraceNotifier : ITraceNotifier
        {
            public List<TraceSummary> Notifications { get; } = [];

            public Task PublishAsync(TraceSummary trace, CancellationToken cancellationToken = default)
            {
                Notifications.Add(trace);
                return Task.CompletedTask;
            }
        }
    }
}