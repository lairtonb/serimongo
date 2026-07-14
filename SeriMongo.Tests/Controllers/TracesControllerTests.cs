using Microsoft.Extensions.DependencyInjection;
using SeriMongo.Data;
using SeriMongo.Models;
using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace SeriMongo.Tests.Controllers
{
    public sealed class TracesControllerTests : IClassFixture<SeriMongoFixture>
    {
        private const string TraceId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private readonly SeriMongoFixture _fixture;

        public TracesControllerTests(SeriMongoFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Search_AppliesFiltersAndReturnsPagedTraces()
        {
            await SeedTraceAsync();
            using var client = _fixture.Factory.CreateClient();

            var response = await client.GetAsync(
                "/api/traces?serviceName=trace-api&status=Successful&page=1&pageSize=10",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await response.Content.ReadFromJsonAsync<TraceSearchPage>(TestContext.Current.CancellationToken);
            var trace = Assert.Single(page.Items);
            Assert.Equal(TraceId, trace.TraceId);
            Assert.Equal(1, page.Page);
            Assert.Equal(10, page.PageSize);
        }

        [Fact]
        public async Task GetTrace_ValidatesIdsAndDistinguishesMissingTraces()
        {
            await SeedTraceAsync();
            using var client = _fixture.Factory.CreateClient();

            var invalidResponse = await client.GetAsync("/api/traces/not-a-trace-id", TestContext.Current.CancellationToken);
            var missingResponse = await client.GetAsync(
                "/api/traces/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                TestContext.Current.CancellationToken);
            var foundResponse = await client.GetAsync($"/api/traces/{TraceId}", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, foundResponse.StatusCode);
            var trace = await foundResponse.Content.ReadFromJsonAsync<TraceDetail>(TestContext.Current.CancellationToken);
            Assert.Equal(TraceId, trace.Summary.TraceId);
            Assert.Equal(2, trace.Spans.Count);
        }

        [Fact]
        public async Task GetServiceNames_ReturnsTraceServices()
        {
            await SeedTraceAsync();
            using var client = _fixture.Factory.CreateClient();

            var services = await client.GetFromJsonAsync<string[]>("/api/traces/services", TestContext.Current.CancellationToken);

            Assert.Contains("trace-api", services);
            Assert.Contains("trace-worker", services);
        }

        private async Task SeedTraceAsync()
        {
            var repository = _fixture.Factory.Services.GetRequiredService<ITraceRepository>();
            var startTimestamp = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
            await repository.UpsertAsync(
                new[]
                {
                    new TraceSpan
                    {
                        TraceId = TraceId,
                        SpanId = "1111111111111111",
                        Name = "POST /api/orders",
                        ServiceName = "trace-api",
                        StartTimestamp = startTimestamp,
                        EndTimestamp = startTimestamp.AddMilliseconds(100),
                        DurationNanoseconds = 100_000_000,
                        StatusCode = 1
                    },
                    new TraceSpan
                    {
                        TraceId = TraceId,
                        SpanId = "2222222222222222",
                        ParentSpanId = "1111111111111111",
                        Name = "Process order",
                        ServiceName = "trace-worker",
                        StartTimestamp = startTimestamp.AddMilliseconds(10),
                        EndTimestamp = startTimestamp.AddMilliseconds(90),
                        DurationNanoseconds = 80_000_000,
                        StatusCode = 1
                    }
                },
                TestContext.Current.CancellationToken);
        }
    }
}