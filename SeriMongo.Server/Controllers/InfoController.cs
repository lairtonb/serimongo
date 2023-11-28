using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SeriMongo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InfoController: ControllerBase
    {
        private readonly ILogger<InfoController> _logger;
        

        public InfoController(ILogger<InfoController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get()
        {
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
