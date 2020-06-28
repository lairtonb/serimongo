using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog.Core;

namespace SeriMongo.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InfoController : ControllerBase
    {
         private readonly ILogger<InfoController> _logger;
        private readonly LoggingLevelSwitch _loggingLevelSwitch;

        public InfoController(ILogger<InfoController> logger, LoggingLevelSwitch loggingLevelSwitch)
        {
            _logger = logger;
            _loggingLevelSwitch = loggingLevelSwitch;
        }

        [HttpGet("logdebug")]
        public void SetLevel(string minimumLevel)
        {
            _loggingLevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
            _logger.LogDebug("This will now be logged");
        }

        [HttpGet]
        public IActionResult Get()
        {
            _logger.LogDebug("This is not logged if Verbose is not enabled.");
            _logger.LogInformation("Teste {Teste}", "This is a test value");
            return Ok(new
                {
                    Environment.MachineName,
                    OSVersion = Environment.OSVersion.VersionString,
                    TargetFramework = Assembly
                        .GetEntryAssembly()?
                        .GetCustomAttribute<TargetFrameworkAttribute>()?
                        .FrameworkName
            });
        }
    }
}
