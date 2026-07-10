using SeriMongo.Data;
using SeriMongo.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Services
{
    public interface ILogIngestService
    {
        Task<LogEntry> IngestAsync(LogEntry logEntry, CancellationToken cancellationToken = default);
    }

    public class LogIngestService : ILogIngestService
    {
        private readonly ILogRepository _logRepository;
        private readonly ILogEntryNotifier _logEntryNotifier;
        private readonly ILogServiceNameCatalog _serviceNameCatalog;

        public LogIngestService(ILogRepository logRepository, ILogEntryNotifier logEntryNotifier, ILogServiceNameCatalog serviceNameCatalog)
        {
            _logRepository = logRepository;
            _logEntryNotifier = logEntryNotifier;
            _serviceNameCatalog = serviceNameCatalog;
        }

        public async Task<LogEntry> IngestAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
        {
            await _serviceNameCatalog.EnsureInitializedAsync(cancellationToken);
            await _logRepository.AddAsync(logEntry, cancellationToken);
            if (await _serviceNameCatalog.AddFromLogEntryAsync(logEntry, cancellationToken))
            {
                await _logEntryNotifier.PublishServiceNamesAsync(_serviceNameCatalog.GetSnapshot(), cancellationToken);
            }

            await _logEntryNotifier.PublishAsync(logEntry, cancellationToken);
            return logEntry;
        }
    }
}
