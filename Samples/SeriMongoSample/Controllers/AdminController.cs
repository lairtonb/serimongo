using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog.Core;

namespace SerilogSample.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly ILogger<AdminController> _logger;
        private readonly LoggingLevelSwitch _loggingLevelSwitch;

        public AdminController(ILogger<AdminController> logger, LoggingLevelSwitch loggingLevelSwitch)
        {
            _logger = logger;
            _loggingLevelSwitch = loggingLevelSwitch;
        }

        [HttpPost()]
        public void SetLevel(Serilog.Events.LogEventLevel minimumLevel)
        {
            _loggingLevelSwitch.MinimumLevel = minimumLevel;
        }
    }
}
