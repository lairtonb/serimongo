using Microsoft.AspNetCore.SignalR;
using SeriMongo.Hubs;
using SeriMongo.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Services
{
    public interface ITraceNotifier
    {
        Task PublishAsync(TraceSummary trace, CancellationToken cancellationToken = default);
    }

    public sealed class SignalRTraceNotifier : ITraceNotifier
    {
        private readonly IHubContext<LoggingHub> _loggingHub;

        public SignalRTraceNotifier(IHubContext<LoggingHub> loggingHub)
        {
            _loggingHub = loggingHub;
        }

        public Task PublishAsync(TraceSummary trace, CancellationToken cancellationToken = default)
        {
            return _loggingHub.Clients.All.SendAsync("OnTraceUpdated", trace, cancellationToken);
        }
    }
}