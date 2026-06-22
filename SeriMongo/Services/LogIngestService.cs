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

        public LogIngestService(ILogRepository logRepository, ILogEntryNotifier logEntryNotifier)
        {
            _logRepository = logRepository;
            _logEntryNotifier = logEntryNotifier;
        }

        public async Task<LogEntry> IngestAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
        {
            await _logRepository.AddAsync(logEntry, cancellationToken);
            await _logEntryNotifier.PublishAsync(logEntry, cancellationToken);
            return logEntry;
        }
    }
}
