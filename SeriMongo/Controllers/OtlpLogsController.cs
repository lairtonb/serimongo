using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SeriMongo.Services;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Controllers
{
    [ApiController]
    public class OtlpLogsController : ControllerBase
    {
        private readonly ILogIngestService _logIngestService;
        private readonly OtlpLogMapper _otlpLogMapper;
        private readonly ILogger<OtlpLogsController> _logger;

        public OtlpLogsController(
            ILogIngestService logIngestService,
            OtlpLogMapper otlpLogMapper,
            ILogger<OtlpLogsController> logger)
        {
            _logIngestService = logIngestService;
            _otlpLogMapper = otlpLogMapper;
            _logger = logger;
        }

        [HttpPost("/v1/logs")]
        [HttpPost("/otlp/v1/logs")]
        public async Task<IActionResult> Receive(CancellationToken cancellationToken)
        {
            if (!IsJsonRequest(Request.ContentType))
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, new
                {
                    Error = "This receiver supports OTLP/HTTP JSON. Configure exporters with OTEL_EXPORTER_OTLP_PROTOCOL=http/json."
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

        private static bool IsJsonRequest(string contentType)
        {
            return string.IsNullOrWhiteSpace(contentType)
                || contentType.Split(';').First().IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
