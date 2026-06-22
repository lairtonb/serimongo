using Microsoft.AspNetCore.SignalR;
using SeriMongo.Hubs;
using SeriMongo.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Services
{
    public interface ILogEntryNotifier
    {
        Task PublishAsync(LogEntry logEntry, CancellationToken cancellationToken = default);
    }

    public class SignalRLogEntryNotifier : ILogEntryNotifier
    {
        private readonly IHubContext<LoggingHub> _loggingHub;

        public SignalRLogEntryNotifier(IHubContext<LoggingHub> loggingHub)
        {
            _loggingHub = loggingHub;
        }

        public Task PublishAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
        {
            return _loggingHub.Clients.All.SendAsync("OnReceiveLogEntry", logEntry, cancellationToken);
        }
    }
}
