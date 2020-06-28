using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.OData;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using SeriMongo.Data;
using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SeriMongo.Controllers
{
    [Route("odata/[controller]")]
    public class AppLogsOdataController: ODataController
    {
        private readonly AppLogsContext _logsContext;
        private readonly ILogger<AppLogsOdataController> _logger;

        public AppLogsOdataController(ILogger<AppLogsOdataController> logger,
            AppLogsContext logsContext)
        {
            _logsContext = logsContext;
            _logger = logger;
        }

        [HttpGet]
        [EnableQuery(        
            PageSize = 20,            
            AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.Skip | AllowedQueryOptions.Top | AllowedQueryOptions.OrderBy,
            AllowedOrderByProperties = "Id,Level,RenderedMessage,Timestamp",
            // I could not make any function work with MongoDB
            AllowedFunctions = AllowedFunctions.None,
            // Disable the any and all functions, as these can be resource-intensive
            // AllowedFunctions = AllowedFunctions.AllFunctions & ~AllowedFunctions.All & ~AllowedFunctions.Any
            AllowedLogicalOperators = AllowedLogicalOperators.All,
            AllowedArithmeticOperators = AllowedArithmeticOperators.None, // Not tested
            HandleNullPropagation = HandleNullPropagationOption.True
        )]
        public ActionResult<IQueryable<LogEntry>> GetTodoItems(ODataQueryOptions<LogEntry> options)
        {
            try
            {
                var q = _logsContext.LogEtries.AsQueryable() as IQueryable<LogEntry>;
                return Ok(q);
            }
            catch (ODataException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("Tests")]
        public ActionResult<IQueryable<LogEntry>> Get(ODataQueryOptions<LogEntry> options)
        {
            var settings = new ODataValidationSettings
            {
                AllowedFunctions = AllowedFunctions.None,
                AllowedLogicalOperators = AllowedLogicalOperators.Equal,
                AllowedArithmeticOperators = AllowedArithmeticOperators.None,
                AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.Select
            };

            try
            {
                options.Validate(settings);
            }
            catch (ODataException ex)
            {
                // System.Net.HttpStatusCode.BadRequest
                return BadRequest(ex.Message);
            }

            var items = options.ApplyTo((from logEntry in _logsContext.LogEtries.AsQueryable() select logEntry).AsQueryable());
            return Ok(items);
        }


    }
}
