using SeriMongo.Data;
using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Services
{
    public interface ILogServiceNameCatalog
    {
        Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

        Task<bool> AddFromLogEntryAsync(LogEntry logEntry, CancellationToken cancellationToken = default);

        IReadOnlyList<string> GetSnapshot();
    }

    public class LogServiceNameCatalog : ILogServiceNameCatalog
    {
        public const string ServiceNameProperty = "resource.service.name";

        private readonly ILogRepository _logRepository;
        private readonly object _gate = new object();
        private readonly SortedSet<string> _serviceNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _initializeLock = new SemaphoreSlim(1, 1);
        private volatile bool _initialized;

        public LogServiceNameCatalog(ILogRepository logRepository)
        {
            _logRepository = logRepository;
        }

        public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
            {
                return;
            }

            await _initializeLock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized)
                {
                    return;
                }

                var serviceNames = await _logRepository.GetServiceNamesAsync(cancellationToken);
                lock (_gate)
                {
                    foreach (var serviceName in serviceNames)
                    {
                        _serviceNames.Add(serviceName);
                    }

                    _initialized = true;
                }
            }
            finally
            {
                _initializeLock.Release();
            }
        }

        public async Task<bool> AddFromLogEntryAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            var serviceName = ExtractServiceName(logEntry);
            if (serviceName == null)
            {
                return false;
            }

            lock (_gate)
            {
                return _serviceNames.Add(serviceName);
            }
        }

        public IReadOnlyList<string> GetSnapshot()
        {
            lock (_gate)
            {
                return _serviceNames.ToArray();
            }
        }

        private static string ExtractServiceName(LogEntry logEntry)
        {
            if (logEntry.Properties == null || !logEntry.Properties.TryGetValue(ServiceNameProperty, out var value))
            {
                return null;
            }

            var serviceName = ConvertServiceNameValue(value)?.Trim();
            return string.IsNullOrEmpty(serviceName) ? null : serviceName;
        }

        private static string ConvertServiceNameValue(object value)
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => jsonElement.ToString(),
                    _ => null
                };
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
