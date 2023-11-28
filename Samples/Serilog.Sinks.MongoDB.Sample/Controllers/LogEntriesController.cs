using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SerilogSample.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogEntriesController : ControllerBase
    {
        private readonly ILogger<LogEntriesController> _logger;
        public LogEntriesController(ILogger<LogEntriesController> logger)
        {
            _logger = logger;
        }

        [HttpPost("LogDebug")]
        public void LogDebug()
        {
            _logger.LogDebug("This is not logged if Verbose is not enabled.");
        }

        [HttpPost("LogInformation")]
        public void LogInformation()
        {
            _logger.LogInformation("Teste {Teste}", "This is a test value");
        }

        [HttpPost("LogWarning")]
        public void LogWarning()
        {
            _logger.LogWarning("Teste {LogWarning}", "This is a warning!");
        }

        [HttpPost("LogError")]
        public void LogError()
        {
            try
            {
                var exception = new Exception("This is a sample error");
                exception.Data.Add("Item1", "This is sample additional data added to the exception");
                throw exception;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "This is an error: {ErrorMessage}", ex.Message);
            }
            
        }
    }
}
