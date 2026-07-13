using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using SeriMongo.Controllers;
using SeriMongo.Models;
using SeriMongo.Services;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SeriMongo.Tests.Controllers
{
    public class OtlpLogsControllerTests
    {
        [Fact]
        public async Task Receive_WithProtobufPayload_IngestsLogAndReturnsOk()
        {
            var ingestService = new CapturingLogIngestService();
            var controller = CreateController(ingestService, CreatePayload().ToByteArray());

            var result = await controller.Receive(CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
            var logEntry = Assert.Single(ingestService.LogEntries);
            Assert.Equal("Error", logEntry.Level);
            Assert.Equal("OTLP protobuf test", logEntry.RenderedMessage);
            Assert.Equal("serimongo-tests", logEntry.Properties["resource.service.name"]);
            Assert.Equal("test-scope", logEntry.Properties["scope.name"]);
            Assert.Equal(42L, logEntry.Properties["CustomerId"]);
        }

        [Fact]
        public async Task Receive_WithInvalidProtobufPayload_ReturnsBadRequest()
        {
            var ingestService = new CapturingLogIngestService();
            var controller = CreateController(ingestService, new byte[] { 0x0A, 0x05, 0x01 });

            var result = await controller.Receive(CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(ingestService.LogEntries);
        }

        private static OtlpLogsController CreateController(
            CapturingLogIngestService ingestService,
            byte[] payload)
        {
            var controller = new OtlpLogsController(
                ingestService,
                new OtlpLogMapper(),
                NullLogger<OtlpLogsController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            controller.Request.ContentType = "application/x-protobuf";
            controller.Request.Body = new MemoryStream(payload);
            return controller;
        }

        private static ExportLogsServiceRequest CreatePayload()
        {
            var logRecord = new LogRecord
            {
                TimeUnixNano = 1_700_000_000_000_000_000UL,
                SeverityText = "ERROR",
                Body = new AnyValue { StringValue = "OTLP protobuf test" }
            };
            logRecord.Attributes.Add(new KeyValue
            {
                Key = "CustomerId",
                Value = new AnyValue { IntValue = 42 }
            });

            var scopeLogs = new ScopeLogs
            {
                Scope = new InstrumentationScope { Name = "test-scope" }
            };
            scopeLogs.LogRecords.Add(logRecord);

            var resourceLogs = new ResourceLogs
            {
                Resource = new Resource()
            };
            resourceLogs.Resource.Attributes.Add(new KeyValue
            {
                Key = "service.name",
                Value = new AnyValue { StringValue = "serimongo-tests" }
            });
            resourceLogs.ScopeLogs.Add(scopeLogs);

            var request = new ExportLogsServiceRequest();
            request.ResourceLogs.Add(resourceLogs);
            return request;
        }

        private sealed class CapturingLogIngestService : ILogIngestService
        {
            public List<LogEntry> LogEntries { get; } = new List<LogEntry>();

            public Task<LogEntry> IngestAsync(
                LogEntry logEntry,
                CancellationToken cancellationToken = default)
            {
                LogEntries.Add(logEntry);
                return Task.FromResult(logEntry);
            }
        }
    }
}
