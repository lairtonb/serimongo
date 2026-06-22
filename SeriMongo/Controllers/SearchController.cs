using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SeriMongo.Data;
using SeriMongo.Models;
using SeriMongo.Querying;
using System.Collections.Generic;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;

namespace SeriMongo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly ILogRepository _logRepository;
        private readonly LogQueryCompiler _logQueryCompiler;
        private readonly ILogger<SearchController> _logger;

        public SearchController(ILogger<SearchController> logger,
            ILogRepository logRepository,
            LogQueryCompiler logQueryCompiler)
        {
            _logRepository = logRepository;
            _logQueryCompiler = logQueryCompiler;
            _logger = logger;
        }

        [HttpGet("dialect")]
        public IActionResult Dialect()
        {
            return Ok(new
            {
                Name = "LogQL",
                Operators = new[] { "=", "!=", ">", ">=", "<", "<=", "contains", "startswith", "endswith", "in", "exists", "and", "or" },
                Fields = new[] { "id", "timestamp", "level", "message", "exception", "prop.<name>" },
                LogQueryCompiler.Examples
            });
        }

        [HttpPost]
        public async Task<ActionResult<IEnumerable<LogEntry>>> Search([FromBody] JsonElement queryText, int currentPage = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                var compiledQuery = _logQueryCompiler.Compile(ExtractQuery(queryText));
                var result = await _logRepository.SearchAsync(compiledQuery, currentPage, pageSize, cancellationToken);
                return Ok(result);
            }
            catch (LogQueryException ex)
            {
                _logger.LogWarning(ex, "Invalid log query");
                return BadRequest(new { Error = ex.Message, LogQueryCompiler.Examples });
            }
        }

        private static string ExtractQuery(JsonElement queryText)
        {
            if (queryText.ValueKind == JsonValueKind.String)
            {
                return queryText.GetString();
            }

            if (queryText.ValueKind == JsonValueKind.Object)
            {
                if (!queryText.EnumerateObject().MoveNext())
                {
                    return "*";
                }

                if (queryText.TryGetProperty("query", out var queryProperty) && queryProperty.ValueKind == JsonValueKind.String)
                {
                    return queryProperty.GetString();
                }
            }

            return queryText.ToString();
        }
    }
}
