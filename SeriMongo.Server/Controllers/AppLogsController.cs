using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using SeriMongo.Data;
using SeriMongo.Models;

namespace SeriMongo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppLogsController : ControllerBase
    {
        private readonly AppLogsContext _logsContext;
        private readonly ILogger<AppLogsController> _logger;

        public AppLogsController(ILogger<AppLogsController> logger,
            AppLogsContext logsContext)
        {
            _logsContext = logsContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IEnumerable<LogEntry>> GetAll(int currentPage = 1, int pageSize = 2)
        {
            var filter = FilterDefinition<LogEntry>.Empty;
            var options = new FindOptions<LogEntry>
            {
                BatchSize = 5,
                Limit = pageSize,
                Skip = currentPage - 1,
                Sort = Builders<LogEntry>.Sort.Descending(field => field.Timestamp),
                // Projection = Builders<LogEntry>.Projection.Include("item").Include("status"),
                /*Projection = Builders<LogEntry>.Projection.Expression(p => new LogEntry
                {
                    RenderedMessage = p.RenderedMessage
                }),*/
                NoCursorTimeout = false
            };
            var list = await _logsContext.LogEtries.FindAsync(filter, options);

            return list.ToList();

            /*
            // Implement projection and return result with pageCount
            double totalDocuments = await collection.CountAsync(FilterDefinition<Student>.Empty);
            var totalPages = Math.Ceiling(totalDocuments / pageSize);             
            */
        }



        /*
        [HttpGet("blah")]
        public ActionResult Blah(string applicationName = "*", string level = "*", IDictionary<string, object> propertyFilter = default(IDictionary<string, object>))
        {
            return Ok(
                new[]
                {
                    new LogEntry
                    {
                        // Id = Guid.NewGuid(),
                        Level = "Information",
                        RenderedMessage = "Teste",
                        Timestamp = DateTime.Now.ToUniversalTime()
                    }
                }
            );
        }
        */
    }
}
