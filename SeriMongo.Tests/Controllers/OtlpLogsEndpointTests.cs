using Google.Protobuf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Logs.V1;
using SeriMongo.Models;
using SeriMongo.Services;
using SeriMongo.Tests.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SeriMongo.Tests.Controllers
{
    public sealed class CapturingIngestService : ILogIngestService
    {
        public List<LogEntry> Entries { get; } = new List<LogEntry>();

        public Task<LogEntry> IngestAsync(
            LogEntry logEntry,
            CancellationToken cancellationToken = default)
        {
            lock (Entries)
            {
                Entries.Add(logEntry);
            }

            return Task.FromResult(logEntry);
        }
    }

    public sealed class SeriMongoFixture : IDisposable
    {
        public SeriMongoFixture()
        {
            DataDirectory = Path.Combine(
                Path.GetTempPath(),
                "serimongo-tests",
                Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(DataDirectory);
            Capture = new CapturingIngestService();

            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.UseSetting(
                    "ApplicationOptions:Database:ConnectionString",
                    $"Data Source={Path.Combine(DataDirectory, "test.db")}");
                builder.UseSetting("ApplicationOptions:Seed:Enabled", "false");
                builder.ConfigureServices(services =>
                    services.Replace(ServiceDescriptor.Singleton<ILogIngestService>(Capture)));
            });
        }

        public string DataDirectory { get; }

        public CapturingIngestService Capture { get; }

        public WebApplicationFactory<Program> Factory { get; }

        public void Dispose()
        {
            Factory.Dispose();

            try
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
            catch (IOException)
            {
                // SQLite may briefly retain the file on Windows.
            }
        }
    }

    public class OtlpLogsEndpointTests : IClassFixture<SeriMongoFixture>
    {
        private readonly SeriMongoFixture _fixture;

        public OtlpLogsEndpointTests(SeriMongoFixture fixture)
        {
            _fixture = fixture;

            lock (_fixture.Capture.Entries)
            {
                _fixture.Capture.Entries.Clear();
            }
        }

        [Theory]
        [InlineData("/v1/logs")]
        [InlineData("/otlp/v1/logs")]
        public async Task ProtobufPayload_IsAcceptedAndIngested(string path)
        {
            using var client = _fixture.Factory.CreateClient();
            var request = OtlpProtobufLogMapperTests.BuildRequest(body: "hello protobuf");
            using var content = CreateProtobufContent(request.ToByteArray());

            var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/x-protobuf", response.Content.Headers.ContentType.MediaType);

            var responseBytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
            var exportResponse = ExportLogsServiceResponse.Parser.ParseFrom(responseBytes);
            Assert.Null(exportResponse.PartialSuccess);

            var entry = Assert.Single(_fixture.Capture.Entries);
            Assert.Equal("hello protobuf", entry.RenderedMessage);
            Assert.Equal("sample-api", entry.Properties["resource.service.name"]);
        }

        [Fact]
        public async Task EmptyProtobufPayload_IsAccepted()
        {
            using var client = _fixture.Factory.CreateClient();
            using var content = CreateProtobufContent(new ExportLogsServiceRequest().ToByteArray());

            var response = await client.PostAsync(
                "/v1/logs",
                content,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(_fixture.Capture.Entries);
        }

        [Fact]
        public async Task JsonPayload_StillWorks()
        {
            using var client = _fixture.Factory.CreateClient();
            const string json = """
                {
                  "resourceLogs": [{
                    "resource": { "attributes": [{ "key": "service.name", "value": { "stringValue": "json-api" } }] },
                    "scopeLogs": [{
                      "logRecords": [{ "severityText": "ERROR", "body": { "stringValue": "hello json" } }]
                    }]
                  }]
                }
                """;
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                "/v1/logs",
                content,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var entry = Assert.Single(_fixture.Capture.Entries);
            Assert.Equal("hello json", entry.RenderedMessage);
            Assert.Equal("Error", entry.Level);
        }

        [Fact]
        public async Task UnsupportedContentType_Returns415()
        {
            using var client = _fixture.Factory.CreateClient();
            using var content = new StringContent("<xml/>", Encoding.UTF8, "text/xml");

            var response = await client.PostAsync(
                "/v1/logs",
                content,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            Assert.Empty(_fixture.Capture.Entries);
        }

        [Fact]
        public async Task MalformedProtobuf_Returns400()
        {
            using var client = _fixture.Factory.CreateClient();
            using var content = CreateProtobufContent(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x01 });

            var response = await client.PostAsync(
                "/v1/logs",
                content,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_fixture.Capture.Entries);
        }

        [Theory]
        [InlineData("/v1/traces")]
        [InlineData("/otlp/v1/traces")]
        [InlineData("/v1/metrics")]
        [InlineData("/otlp/v1/metrics")]
        public async Task TraceAndMetricProtobufSignals_AreAccepted(string path)
        {
            using var client = _fixture.Factory.CreateClient();
            using var content = CreateProtobufContent(new byte[] { 0x0A, 0x00 });

            var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/x-protobuf", response.Content.Headers.ContentType.MediaType);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            Assert.Empty(_fixture.Capture.Entries);
        }

        [Theory]
        [InlineData("/v1/metrics")]
        [InlineData("/otlp/v1/metrics")]
        public async Task MetricJsonSignals_AreAcceptedAndDropped(string path)
        {
            using var client = _fixture.Factory.CreateClient();
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(_fixture.Capture.Entries);
        }

        [Theory]
        [InlineData("/v1/traces")]
        [InlineData("/otlp/v1/traces")]
        public async Task TraceJsonPayload_WithoutResourceSpans_ReturnsBadRequest(string path)
        {
            using var client = _fixture.Factory.CreateClient();
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_fixture.Capture.Entries);
        }

        [Fact]
        public async Task TraceWithUnsupportedContentType_Returns415()
        {
            using var client = _fixture.Factory.CreateClient();
            using var content = new StringContent("<xml/>", Encoding.UTF8, "text/xml");

            var response = await client.PostAsync(
                "/v1/traces",
                content,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            Assert.Empty(_fixture.Capture.Entries);
        }

        private static ByteArrayContent CreateProtobufContent(byte[] payload)
        {
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
            return content;
        }
    }
}
