using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SeriMongo.Data;
using SeriMongo.Models;
using SeriMongo.Services;

namespace SeriMongo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppLogsController: ControllerBase
    {
        private readonly ILogRepository _logRepository;
        private readonly ILogIngestService _logIngestService;
        private readonly ILogger<AppLogsController> _logger;

        public AppLogsController(ILogger<AppLogsController> logger,
            ILogRepository logRepository,
            ILogIngestService logIngestService)
        {
            _logRepository = logRepository;
            _logIngestService = logIngestService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IEnumerable<LogEntry>> GetAll(int currentPage = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            return await _logRepository.GetRecentAsync(currentPage, pageSize, cancellationToken);
        }

        [HttpPost]
        public async Task<ActionResult<LogEntry>> Add(LogEntry logEntry, CancellationToken cancellationToken = default)
        {
            await _logIngestService.IngestAsync(logEntry, cancellationToken);
            return CreatedAtAction(nameof(GetAll), new { id = logEntry.Id }, logEntry);
        }
    }
}
