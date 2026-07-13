using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using SeriMongo.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace SeriMongo.Tests.Services
{
    public class OtlpProtobufLogMapperTests
    {
        private readonly OtlpProtobufLogMapper _mapper = new OtlpProtobufLogMapper();

        internal static ExportLogsServiceRequest BuildRequest(
            string body = "Order 42 created",
            SeverityNumber severity = SeverityNumber.Info,
            string severityText = null,
            ulong timeUnixNano = 1_751_500_000_123_456_789UL)
        {
            var record = new LogRecord
            {
                TimeUnixNano = timeUnixNano,
                SeverityNumber = severity,
                SeverityText = severityText ?? string.Empty,
                Body = new AnyValue { StringValue = body },
                TraceId = ByteString.CopyFrom(Convert.FromHexString("0af7651916cd43dd8448eb211c80319c")),
                SpanId = ByteString.CopyFrom(Convert.FromHexString("b7ad6b7169203331"))
            };
            record.Attributes.Add(new KeyValue
            {
                Key = "OrderId",
                Value = new AnyValue { IntValue = 42 }
            });
            record.Attributes.Add(new KeyValue
            {
                Key = "tags",
                Value = new AnyValue
                {
                    ArrayValue = new ArrayValue
                    {
                        Values =
                        {
                            new AnyValue { StringValue = "checkout" },
                            new AnyValue { BoolValue = true }
                        }
                    }
                }
            });
            record.Attributes.Add(new KeyValue
            {
                Key = "context",
                Value = new AnyValue
                {
                    KvlistValue = new KeyValueList
                    {
                        Values =
                        {
                            new KeyValue
                            {
                                Key = "region",
                                Value = new AnyValue { StringValue = "eu-west" }
                            }
                        }
                    }
                }
            });
            record.Attributes.Add(new KeyValue
            {
                Key = "payload",
                Value = new AnyValue { BytesValue = ByteString.CopyFrom(1, 2, 3) }
            });

            var resource = new Resource();
            resource.Attributes.Add(new KeyValue
            {
                Key = "service.name",
                Value = new AnyValue { StringValue = "sample-api" }
            });

            var scope = new InstrumentationScope
            {
                Name = "Sample.Scope",
                Version = "1.2.3"
            };

            return new ExportLogsServiceRequest
            {
                ResourceLogs =
                {
                    new ResourceLogs
                    {
                        Resource = resource,
                        ScopeLogs =
                        {
                            new ScopeLogs
                            {
                                Scope = scope,
                                LogRecords = { record }
                            }
                        }
                    }
                }
            };
        }

        [Fact]
        public void Map_MapsBodySeverityAndTimestamp()
        {
            var entry = Assert.Single(_mapper.Map(BuildRequest()));

            Assert.Equal("Order 42 created", entry.RenderedMessage);
            Assert.Equal("Information", entry.Level);
            Assert.Equal(
                DateTimeOffset.FromUnixTimeSeconds(1_751_500_000).AddTicks(1_234_567),
                entry.Timestamp);
        }

        [Fact]
        public void Map_PrefixesResourceAndScopePropertiesLikeJsonMapper()
        {
            var entry = Assert.Single(_mapper.Map(BuildRequest()));

            Assert.Equal("sample-api", entry.Properties["resource.service.name"]);
            Assert.Equal("Sample.Scope", entry.Properties["scope.name"]);
            Assert.Equal("1.2.3", entry.Properties["scope.version"]);
        }

        [Fact]
        public void Map_MapsAllAttributeValueKinds()
        {
            var entry = Assert.Single(_mapper.Map(BuildRequest()));

            Assert.Equal(42L, entry.Properties["OrderId"]);

            var tags = Assert.IsType<object[]>(entry.Properties["tags"]);
            Assert.Equal(new object[] { "checkout", true }, tags);

            var context = Assert.IsType<Dictionary<string, object>>(entry.Properties["context"]);
            Assert.Equal("eu-west", context["region"]);
            Assert.Equal("AQID", entry.Properties["payload"]);
        }

        [Fact]
        public void Map_MapsTraceContextAsLowercaseHex()
        {
            var entry = Assert.Single(_mapper.Map(BuildRequest()));

            Assert.Equal("0af7651916cd43dd8448eb211c80319c", entry.Properties["traceId"]);
            Assert.Equal("b7ad6b7169203331", entry.Properties["spanId"]);
            Assert.Equal(9L, entry.Properties["severityNumber"]);
        }

        [Theory]
        [InlineData(SeverityNumber.Trace, "Trace")]
        [InlineData(SeverityNumber.Debug, "Debug")]
        [InlineData(SeverityNumber.Info, "Information")]
        [InlineData(SeverityNumber.Warn, "Warning")]
        [InlineData(SeverityNumber.Error, "Error")]
        [InlineData(SeverityNumber.Fatal, "Fatal")]
        public void Map_MapsSeverityNumbers(SeverityNumber severity, string expected)
        {
            var entry = Assert.Single(_mapper.Map(BuildRequest(severity: severity)));

            Assert.Equal(expected, entry.Level);
        }

        [Fact]
        public void Map_SeverityTextWinsOverNumber()
        {
            var entry = Assert.Single(_mapper.Map(BuildRequest(
                severity: SeverityNumber.Info,
                severityText: "WARN")));

            Assert.Equal("Warning", entry.Level);
        }

        [Fact]
        public void Map_ExceptionAttributesPopulateExceptionField()
        {
            var request = BuildRequest();
            request.ResourceLogs[0].ScopeLogs[0].LogRecords[0].Attributes.Add(new KeyValue
            {
                Key = "exception.stacktrace",
                Value = new AnyValue
                {
                    StringValue = "System.InvalidOperationException: boom\n at X"
                }
            });

            var entry = Assert.Single(_mapper.Map(request));

            Assert.StartsWith("System.InvalidOperationException", entry.Exception);
        }

        [Fact]
        public void Map_MissingTimestampFallsBackToNow()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-5);

            var entry = Assert.Single(_mapper.Map(BuildRequest(timeUnixNano: 0UL)));

            Assert.InRange(entry.Timestamp, before, DateTimeOffset.UtcNow.AddSeconds(5));
        }

        [Fact]
        public void Map_EmptyRequestReturnsNoEntries()
        {
            var entries = _mapper.Map(new ExportLogsServiceRequest());

            Assert.Empty(entries);
        }

        [Fact]
        public void Map_ProtobufAndJsonPathsProduceEquivalentEntries()
        {
            var request = BuildRequest(severityText: "INFO");
            var protobufEntry = Assert.Single(_mapper.Map(request));

            var json = JsonFormatter.Default.Format(request);
            using var document = JsonDocument.Parse(json);
            var jsonEntry = Assert.Single(new OtlpLogMapper().Map(document));

            Assert.Equal(jsonEntry.RenderedMessage, protobufEntry.RenderedMessage);
            Assert.Equal(jsonEntry.Level, protobufEntry.Level);
            Assert.Equal(jsonEntry.Timestamp, protobufEntry.Timestamp);
            Assert.Equal(
                jsonEntry.Properties["resource.service.name"],
                protobufEntry.Properties["resource.service.name"]);
            Assert.Equal(jsonEntry.Properties["scope.name"], protobufEntry.Properties["scope.name"]);
            Assert.Equal(
                Convert.ToInt64(jsonEntry.Properties["OrderId"]),
                Convert.ToInt64(protobufEntry.Properties["OrderId"]));
        }
    }
}
