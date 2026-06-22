using Microsoft.AspNetCore.SignalR;
using SeriMongo.Models;
using SeriMongo.Services;
using System.Threading.Tasks;

namespace SeriMongo.Hubs
{
    public class LoggingHub: Hub
    {
        private readonly ILogIngestService _logIngestService;

        public LoggingHub(ILogIngestService logIngestService)
        {
            _logIngestService = logIngestService;
        }

        public async Task SendLogEntry(LogEntry logEntry)
        {
            await _logIngestService.IngestAsync(logEntry, Context.ConnectionAborted);
        }
    }



}
