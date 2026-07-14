using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Trace.V1;
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
    public sealed class OtlpTracesController : ControllerBase
    {
        private const string ProtobufContentType = "application/x-protobuf";

        private readonly ITraceIngestService _traceIngestService;
        private readonly OtlpTraceMapper _otlpTraceMapper;
        private readonly OtlpProtobufTraceMapper _otlpProtobufTraceMapper;
        private readonly ILogger<OtlpTracesController> _logger;

        public OtlpTracesController(
            ITraceIngestService traceIngestService,
            OtlpTraceMapper otlpTraceMapper,
            OtlpProtobufTraceMapper otlpProtobufTraceMapper,
            ILogger<OtlpTracesController> logger)
        {
            _traceIngestService = traceIngestService;
            _otlpTraceMapper = otlpTraceMapper;
            _otlpProtobufTraceMapper = otlpProtobufTraceMapper;
            _logger = logger;
        }

        [HttpPost("/v1/traces")]
        [HttpPost("/otlp/v1/traces")]
        public async Task<IActionResult> Receive(CancellationToken cancellationToken)
        {
            if (IsProtobufRequest(Request.ContentType))
            {
                return await ReceiveProtobufAsync(cancellationToken);
            }

            if (!IsJsonRequest(Request.ContentType))
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, new
                {
                    Error = "This receiver supports OTLP/HTTP Protobuf and JSON."
                });
            }

            try
            {
                using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
                var result = await _traceIngestService.IngestAsync(_otlpTraceMapper.Map(document), cancellationToken);
                _logger.LogInformation("Persisted {Count} OTLP spans and rejected {RejectedCount}", result.PersistedSpans, result.RejectedSpans);
                return Ok(CreateResponse(result));
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
            {
                _logger.LogWarning(ex, "Invalid OTLP trace payload");
                return BadRequest(new { Error = ex.Message });
            }
        }

        private async Task<IActionResult> ReceiveProtobufAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var payload = new MemoryStream();
                await Request.Body.CopyToAsync(payload, cancellationToken);
                payload.Position = 0;

                var request = ExportTraceServiceRequest.Parser.ParseFrom(payload);
                var result = await _traceIngestService.IngestAsync(_otlpProtobufTraceMapper.Map(request), cancellationToken);
                _logger.LogInformation("Persisted {Count} OTLP spans and rejected {RejectedCount}", result.PersistedSpans, result.RejectedSpans);

                var response = new ExportTraceServiceResponse();
                if (result.RejectedSpans > 0)
                {
                    response.PartialSuccess = new ExportTracePartialSuccess
                    {
                        RejectedSpans = result.RejectedSpans,
                        ErrorMessage = result.ErrorMessage ?? string.Empty
                    };
                }

                return File(response.ToByteArray(), ProtobufContentType);
            }
            catch (InvalidProtocolBufferException ex)
            {
                _logger.LogWarning(ex, "Invalid OTLP protobuf trace payload");
                return BadRequest(new { Error = ex.Message });
            }
        }

        private static object CreateResponse(TraceIngestResult result)
        {
            return result.RejectedSpans == 0
                ? new { }
                : new
                {
                    partialSuccess = new
                    {
                        rejectedSpans = result.RejectedSpans,
                        errorMessage = result.ErrorMessage ?? string.Empty
                    }
                };
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
