using Microsoft.AspNetCore.SignalR;
using SeriMongo.Models;
using SeriMongo.Services;
using System.Threading.Tasks;

namespace SeriMongo.Hubs
{
    public class LoggingHub: Hub
    {
        private readonly ILogIngestService _logIngestService;
        private readonly ILogServiceNameCatalog _serviceNameCatalog;

        public LoggingHub(ILogIngestService logIngestService, ILogServiceNameCatalog serviceNameCatalog)
        {
            _logIngestService = logIngestService;
            _serviceNameCatalog = serviceNameCatalog;
        }

        public override async Task OnConnectedAsync()
        {
            await _serviceNameCatalog.EnsureInitializedAsync(Context.ConnectionAborted);
            await Clients.Caller.SendAsync("OnReceiveServiceNames", _serviceNameCatalog.GetSnapshot(), Context.ConnectionAborted);
            await base.OnConnectedAsync();
        }

        public async Task SendLogEntry(LogEntry logEntry)
        {
            await _logIngestService.IngestAsync(logEntry, Context.ConnectionAborted);
        }
    }



}
