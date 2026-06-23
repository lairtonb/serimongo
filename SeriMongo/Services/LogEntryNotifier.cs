using Microsoft.AspNetCore.SignalR;
using SeriMongo.Hubs;
using SeriMongo.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Services
{
    public interface ILogEntryNotifier
    {
        Task PublishAsync(LogEntry logEntry, CancellationToken cancellationToken = default);

        Task PublishServiceNamesAsync(IReadOnlyList<string> serviceNames, CancellationToken cancellationToken = default);
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

        public Task PublishServiceNamesAsync(IReadOnlyList<string> serviceNames, CancellationToken cancellationToken = default)
        {
            return _loggingHub.Clients.All.SendAsync("OnReceiveServiceNames", serviceNames, cancellationToken);
        }
    }
}
