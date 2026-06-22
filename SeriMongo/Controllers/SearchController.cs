using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SeriMongo.Data;
using SeriMongo.Models;
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
        private readonly ILogger<SearchController> _logger;

        public SearchController(ILogger<SearchController> logger,
            ILogRepository logRepository)
        {
            _logRepository = logRepository;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IEnumerable<LogEntry>> Search([FromBody] JsonElement queryText, int currentPage = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            return await _logRepository.GetRecentAsync(currentPage, pageSize, cancellationToken);
        }
    }
}
