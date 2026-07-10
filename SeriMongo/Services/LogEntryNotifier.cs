using Microsoft.AspNetCore.SignalR;
using SeriMongo.Data;
using SeriMongo.Hubs;
using SeriMongo.Models;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ILogRepository _logRepository;
        private readonly ITailSubscriptionStore _tailSubscriptionStore;

        public SignalRLogEntryNotifier(IHubContext<LoggingHub> loggingHub, ILogRepository logRepository, ITailSubscriptionStore tailSubscriptionStore)
        {
            _loggingHub = loggingHub;
            _logRepository = logRepository;
            _tailSubscriptionStore = tailSubscriptionStore;
        }

        public async Task PublishAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
        {
            var subscriptions = _tailSubscriptionStore.GetSnapshot();
            if (subscriptions.Count == 0)
            {
                return;
            }

            var matchingConnectionIds = new List<string>();
            foreach (var subscription in subscriptions)
            {
                if (await _logRepository.MatchesAsync(logEntry.Id, subscription.Query, cancellationToken))
                {
                    matchingConnectionIds.Add(subscription.ConnectionId);
                }
            }

            if (matchingConnectionIds.Count > 0)
            {
                await _loggingHub.Clients.Clients(matchingConnectionIds.Distinct()).SendAsync("OnReceiveLogEntry", logEntry, cancellationToken);
            }
        }

        public Task PublishServiceNamesAsync(IReadOnlyList<string> serviceNames, CancellationToken cancellationToken = default)
        {
            return _loggingHub.Clients.All.SendAsync("OnReceiveServiceNames", serviceNames, cancellationToken);
        }
    }
}
