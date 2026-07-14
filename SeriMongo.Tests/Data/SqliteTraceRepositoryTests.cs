using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeriMongo.Data;
using SeriMongo.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SeriMongo.Tests.Data
{
    public sealed class SqliteTraceRepositoryTests : IDisposable
    {
        private const string FirstTraceId = "0af7651916cd43dd8448eb211c80319c";
        private const string SecondTraceId = "2bf7651916cd43dd8448eb211c80319c";
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"serimongo-trace-tests-{Guid.NewGuid():N}.db");

        private string ConnectionString => $"Data Source={_databasePath};Pooling=False";

        [Fact]
        public async Task UpsertAsync_ReplayingSpanUpdatesExistingSpanAndSummary()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var repository = CreateRepository();
            var startTimestamp = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

            await repository.UpsertAsync(
                new[]
                {
                    CreateSpan(FirstTraceId, "1111111111111111", null, "initial operation", "checkout", startTimestamp, 50_000_000)
                },
                cancellationToken);
            await repository.UpsertAsync(
                new[]
                {
                    CreateSpan(FirstTraceId, "1111111111111111", null, "updated operation", "checkout", startTimestamp, 75_000_000, statusCode: 2)
                },
                cancellationToken);

            var detail = await repository.GetTraceAsync(FirstTraceId, cancellationToken);

            Assert.NotNull(detail);
            var span = Assert.Single(detail.Spans);
            Assert.Equal("updated operation", span.Name);
            Assert.Equal(1, detail.Summary.SpanCount);
            Assert.Equal(1, detail.Summary.ErrorSpanCount);
            Assert.Equal(75_000_000, detail.Summary.DurationNanoseconds);
        }

        [Fact]
        public async Task UpsertAsync_LateRootSpanRecomputesDerivedSummary()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var repository = CreateRepository();
            var startTimestamp = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

            await repository.UpsertAsync(
                new[]
                {
                    CreateSpan(FirstTraceId, "2222222222222222", "1111111111111111", "child operation", "worker", startTimestamp.AddMilliseconds(50), 50_000_000, statusCode: 2)
                },
                cancellationToken);

            var partialDetail = await repository.GetTraceAsync(FirstTraceId, cancellationToken);
            Assert.Equal("2222222222222222", partialDetail.Summary.RootSpanId);

            await repository.UpsertAsync(
                new[]
                {
                    CreateSpan(FirstTraceId, "1111111111111111", null, "root operation", "api", startTimestamp, 300_000_000)
                },
                cancellationToken);

            var detail = await repository.GetTraceAsync(FirstTraceId, cancellationToken);

            Assert.Equal("1111111111111111", detail.Summary.RootSpanId);
            Assert.Equal("root operation", detail.Summary.RootSpanName);
            Assert.Equal("api", detail.Summary.RootServiceName);
            Assert.Equal(2, detail.Summary.SpanCount);
            Assert.Equal(1, detail.Summary.ErrorSpanCount);
            Assert.Equal(300_000_000, detail.Summary.DurationNanoseconds);
            Assert.Equal(new[] { "api", "worker" }, detail.Summary.ServiceNames);
        }

        [Fact]
        public async Task SearchAsyncAndGetServiceNamesAsyncApplyTraceFilters()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var repository = CreateRepository();
            var startTimestamp = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

            await repository.UpsertAsync(
                new[]
                {
                    CreateSpan(FirstTraceId, "1111111111111111", null, "checkout order", "checkout", startTimestamp, 75_000_000, statusCode: 2),
                    CreateSpan(SecondTraceId, "3333333333333333", null, "charge payment", "billing", startTimestamp.AddMinutes(1), 25_000_000)
                },
                cancellationToken);

            var searchResult = await repository.SearchAsync(
                new TraceSearchFilter
                {
                    ServiceName = "checkout",
                    Status = TraceStatusFilter.Error,
                    MinimumDurationNanoseconds = 50_000_000
                },
                cancellationToken);
            var serviceNames = await repository.GetServiceNamesAsync(cancellationToken);

            var summary = Assert.Single(searchResult.Items);
            Assert.Equal(FirstTraceId, summary.TraceId);
            Assert.Equal(new[] { "billing", "checkout" }, serviceNames);
        }

        public void Dispose()
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }

        private SqliteTraceRepository CreateRepository()
        {
            var context = new AppLogsContext(
                Options.Create(new ApplicationOptions
                {
                    Database = new DatabaseSettings
                    {
                        ConnectionString = ConnectionString
                    }
                }),
                new SqliteSchemaMigrator(NullLogger<SqliteSchemaMigrator>.Instance),
                NullLogger<AppLogsContext>.Instance);

            return new SqliteTraceRepository(context);
        }

        private static TraceSpan CreateSpan(
            string traceId,
            string spanId,
            string parentSpanId,
            string name,
            string serviceName,
            DateTimeOffset startTimestamp,
            long durationNanoseconds,
            int statusCode = 0)
        {
            return new TraceSpan
            {
                TraceId = traceId,
                SpanId = spanId,
                ParentSpanId = parentSpanId,
                Name = name,
                ServiceName = serviceName,
                StartTimestamp = startTimestamp,
                EndTimestamp = startTimestamp.AddTicks(durationNanoseconds / 100),
                DurationNanoseconds = durationNanoseconds,
                StatusCode = statusCode
            };
        }
    }
}
