using Microsoft.AspNetCore.Mvc;
using SeriMongo.Data;
using SeriMongo.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Controllers
{
    [ApiController]
    [Route("api/traces")]
    public sealed class TracesController : ControllerBase
    {
        private readonly ITraceRepository _traceRepository;

        public TracesController(ITraceRepository traceRepository)
        {
            _traceRepository = traceRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Search(
            [FromQuery] TraceSearchFilter filter,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(filter.TraceId) && !IsTraceId(filter.TraceId))
            {
                return BadRequest(new { Error = "Trace IDs must be 32-character lowercase hexadecimal values." });
            }

            if (!Enum.IsDefined(filter.Status))
            {
                return BadRequest(new { Error = "The trace status filter is invalid." });
            }

            return Ok(await _traceRepository.SearchAsync(filter, cancellationToken));
        }

        [HttpGet("services")]
        public async Task<IActionResult> GetServiceNames(CancellationToken cancellationToken)
        {
            return Ok(await _traceRepository.GetServiceNamesAsync(cancellationToken));
        }

        [HttpGet("{traceId}")]
        public async Task<IActionResult> GetTrace(string traceId, CancellationToken cancellationToken)
        {
            if (!IsTraceId(traceId))
            {
                return BadRequest(new { Error = "Trace IDs must be 32-character lowercase hexadecimal values." });
            }

            var trace = await _traceRepository.GetTraceAsync(traceId, cancellationToken);
            return trace is null ? NotFound() : Ok(trace);
        }

        private static bool IsTraceId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
            {
                return false;
            }

            foreach (var character in value)
            {
                if ((character < '0' || character > '9') && (character < 'a' || character > 'f'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}