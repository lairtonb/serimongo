using Microsoft.AspNetCore.SignalR;
using SeriMongo.Models;
using SeriMongo.Querying;
using SeriMongo.Services;
using System;
using System.Threading.Tasks;

namespace SeriMongo.Hubs
{
    public class LoggingHub: Hub
    {
        private readonly ILogIngestService _logIngestService;
        private readonly ILogServiceNameCatalog _serviceNameCatalog;
        private readonly ITailSubscriptionStore _tailSubscriptionStore;
        private readonly LogQueryCompiler _logQueryCompiler;

        public LoggingHub(ILogIngestService logIngestService, ILogServiceNameCatalog serviceNameCatalog, ITailSubscriptionStore tailSubscriptionStore, LogQueryCompiler logQueryCompiler)
        {
            _logIngestService = logIngestService;
            _serviceNameCatalog = serviceNameCatalog;
            _tailSubscriptionStore = tailSubscriptionStore;
            _logQueryCompiler = logQueryCompiler;
        }

        public override async Task OnConnectedAsync()
        {
            await _serviceNameCatalog.EnsureInitializedAsync(Context.ConnectionAborted);
            await Clients.Caller.SendAsync("OnReceiveServiceNames", _serviceNameCatalog.GetSnapshot(), Context.ConnectionAborted);
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            _tailSubscriptionStore.Remove(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        public Task SetTailQuery(string query)
        {
            try
            {
                _tailSubscriptionStore.Set(Context.ConnectionId, query, _logQueryCompiler.Compile(query));
                return Task.CompletedTask;
            }
            catch (LogQueryException ex)
            {
                throw new HubException(ex.Message);
            }
        }

        public async Task SendLogEntry(LogEntry logEntry)
        {
            await _logIngestService.IngestAsync(logEntry, Context.ConnectionAborted);
        }
    }



}
