using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using SeriMongo.Data;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SeriMongo.Tests.Controllers
{
    public sealed class OtlpTracesEndpointTests : IClassFixture<SeriMongoFixture>
    {
        private const string TraceId = "0123456789abcdef0123456789abcdef";
        private const string RootSpanId = "0123456789abcdef";
        private readonly SeriMongoFixture _fixture;

        public OtlpTracesEndpointTests(SeriMongoFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task JsonPayload_IsPersisted()
        {
            const string json = """
                {
                  "resourceSpans": [{
                    "resource": { "attributes": [{ "key": "service.name", "value": { "stringValue": "trace-json-api" } }] },
                    "scopeSpans": [{
                      "scope": { "name": "trace-json-tests", "version": "1.0.0" },
                      "spans": [{
                        "traceId": "0123456789abcdef0123456789abcdef",
                        "spanId": "0123456789abcdef",
                        "name": "POST /orders",
                        "kind": 2,
                        "startTimeUnixNano": "1700000000000000000",
                        "endTimeUnixNano": "1700000000200000000",
                        "status": { "code": 1 }
                      }]
                    }]
                  }]
                }
                """;
            using var client = _fixture.Factory.CreateClient();
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/v1/traces", content, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var repository = _fixture.Factory.Services.GetRequiredService<ITraceRepository>();
            var trace = await repository.GetTraceAsync(TraceId, TestContext.Current.CancellationToken);
            Assert.NotNull(trace);
            var span = Assert.Single(trace.Spans);
            Assert.Equal(RootSpanId, span.SpanId);
            Assert.Equal("trace-json-api", span.ServiceName);
            Assert.Equal("trace-json-tests", span.ScopeName);
        }

        [Fact]
        public async Task ProtobufPayload_WithInvalidSpanId_ReturnsPartialSuccess()
        {
            var request = new ExportTraceServiceRequest();
            var resourceSpans = new ResourceSpans
            {
                Resource = new Resource()
            };
            resourceSpans.Resource.Attributes.Add(new KeyValue
            {
                Key = "service.name",
                Value = new AnyValue { StringValue = "trace-protobuf-api" }
            });

            var scopeSpans = new ScopeSpans
            {
                Scope = new InstrumentationScope { Name = "trace-protobuf-tests" }
            };
            scopeSpans.Spans.Add(new Span
            {
                TraceId = ByteString.CopyFrom(new byte[16]),
                SpanId = ByteString.CopyFrom(new byte[] { 0x01 }),
                Name = "invalid span",
                StartTimeUnixNano = 1_700_000_000_000_000_000UL,
                EndTimeUnixNano = 1_700_000_000_100_000_000UL
            });
            resourceSpans.ScopeSpans.Add(scopeSpans);
            request.ResourceSpans.Add(resourceSpans);

            using var client = _fixture.Factory.CreateClient();
            using var content = new ByteArrayContent(request.ToByteArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");

            var response = await client.PostAsync("/otlp/v1/traces", content, TestContext.Current.CancellationToken);

            Assert.True(
                response.IsSuccessStatusCode,
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var responseBytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
            var exportResponse = ExportTraceServiceResponse.Parser.ParseFrom(responseBytes);
            Assert.NotNull(exportResponse.PartialSuccess);
            Assert.Equal(1, exportResponse.PartialSuccess.RejectedSpans);
        }
    }
}