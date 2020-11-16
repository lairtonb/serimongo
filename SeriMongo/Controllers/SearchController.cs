using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using SeriMongo.Data;
using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SeriMongo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly AppLogsContext _logsContext;
        private readonly ILogger<SearchController> _logger;

        public SearchController(ILogger<SearchController> logger,
            AppLogsContext logsContext)
        {
            _logsContext = logsContext;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IEnumerable<LogEntry>> Search([FromBody] JsonElement queryText, int currentPage = 1, int pageSize = 100)
        {
            var options = new FindOptions<LogEntry>
            {
                BatchSize = 5,
                Limit = pageSize,
                Skip = currentPage - 1,
                Sort = Builders<LogEntry>.Sort.Descending(field => field.Timestamp),
                NoCursorTimeout = false
            };

            // Bson Query
            var bsonDoc = BsonDocument.Parse(queryText.ToString());
            var cursor = await _logsContext.LogEtries.FindAsync<LogEntry>(bsonDoc, options);
            return await cursor.ToListAsync();
        }
    }
}
