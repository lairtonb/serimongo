using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Logs.V1;
using SeriMongo.Services;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Controllers
{
    [ApiController]
    public class OtlpLogsController : ControllerBase
    {
        private const string ProtobufContentType = "application/x-protobuf";

        private readonly ILogIngestService _logIngestService;
        private readonly OtlpLogMapper _otlpLogMapper;
        private readonly OtlpProtobufLogMapper _otlpProtobufLogMapper;
        private readonly ILogger<OtlpLogsController> _logger;

        public OtlpLogsController(
            ILogIngestService logIngestService,
            OtlpLogMapper otlpLogMapper,
            OtlpProtobufLogMapper otlpProtobufLogMapper,
            ILogger<OtlpLogsController> logger)
        {
            _logIngestService = logIngestService;
            _otlpLogMapper = otlpLogMapper;
            _otlpProtobufLogMapper = otlpProtobufLogMapper;
            _logger = logger;
        }

        [HttpPost("/v1/logs")]
        [HttpPost("/otlp/v1/logs")]
        public async Task<IActionResult> Receive(CancellationToken cancellationToken)
        {
            if (IsProtobufRequest(Request.ContentType))
            {
                return await ReceiveProtobuf(cancellationToken);
            }

            if (!IsJsonRequest(Request.ContentType))
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, new
                {
                    Error = "This receiver supports OTLP/HTTP Protobuf and JSON. Configure exporters with OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf or http/json."
                });
            }

            try
            {
                using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
                var logEntries = _otlpLogMapper.Map(document);

                foreach (var logEntry in logEntries)
                {
                    await _logIngestService.IngestAsync(logEntry, cancellationToken);
                }

                _logger.LogInformation("Received {Count} OTLP log records", logEntries.Count);
                return Ok(new { });
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
            {
                _logger.LogWarning(ex, "Invalid OTLP log payload");
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("/v1/metrics")]
        [HttpPost("/otlp/v1/metrics")]
        public IActionResult AcceptAndDrop()
        {
            if (IsProtobufRequest(Request.ContentType))
            {
                return File(Array.Empty<byte>(), ProtobufContentType);
            }

            if (IsJsonRequest(Request.ContentType))
            {
                return Ok(new { });
            }

            return StatusCode(StatusCodes.Status415UnsupportedMediaType, new
            {
                Error = "This receiver supports OTLP/HTTP Protobuf and JSON payloads."
            });
        }

        private async Task<IActionResult> ReceiveProtobuf(CancellationToken cancellationToken)
        {
            try
            {
                using var payload = new MemoryStream();
                await Request.Body.CopyToAsync(payload, cancellationToken);
                payload.Position = 0;

                var request = ExportLogsServiceRequest.Parser.ParseFrom(payload);
                var logEntries = _otlpProtobufLogMapper.Map(request);

                foreach (var logEntry in logEntries)
                {
                    await _logIngestService.IngestAsync(logEntry, cancellationToken);
                }

                _logger.LogInformation("Received {Count} OTLP log records", logEntries.Count);
                return File(new ExportLogsServiceResponse().ToByteArray(), ProtobufContentType);
            }
            catch (InvalidProtocolBufferException ex)
            {
                _logger.LogWarning(ex, "Invalid OTLP protobuf log payload");
                return BadRequest(new { Error = ex.Message });
            }
        }

        private static bool IsProtobufRequest(string contentType)
        {
            return !string.IsNullOrWhiteSpace(contentType)
                && string.Equals(
                    contentType.Split(';').First().Trim(),
                    ProtobufContentType,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsJsonRequest(string contentType)
        {
            return string.IsNullOrWhiteSpace(contentType)
                || contentType.Split(';').First().IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
