using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeriMongo.Models;
using SeriMongo.Querying;
using System;
using System.Collections.Generic;
using System.IO;

namespace SeriMongo.Data
{
    public class AppLogsContext
    {
        private readonly ILogger<AppLogsContext> _logger;

        public AppLogsContext(IOptions<ApplicationOptions> applicationOptions, ILogger<AppLogsContext> logger)
        {
            _logger = logger;
            ConnectionString = applicationOptions.Value.Database.ConnectionString;

            EnsureDatabaseDirectoryExists();
            EnsureCreated();
        }

        public string ConnectionString { get; }

        public SqliteConnection CreateConnection()
        {
            return new SqliteConnection(ConnectionString);
        }

        private void EnsureDatabaseDirectoryExists()
        {
            var builder = new SqliteConnectionStringBuilder(ConnectionString);
            var dataSource = builder.DataSource;

            if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
            {
                return;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void EnsureCreated()
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS LogEntries (
    Id TEXT NOT NULL PRIMARY KEY,
    TimestampUtc TEXT NOT NULL,
    Level TEXT NOT NULL,
    RenderedMessage TEXT NOT NULL,
    Exception TEXT NULL,
    PropertiesJson TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_LogEntries_TimestampUtc ON LogEntries (TimestampUtc DESC);
CREATE INDEX IF NOT EXISTS IX_LogEntries_Level ON LogEntries (Level);
";

            command.ExecuteNonQuery();
            _logger.LogInformation("SQLite log store ready at {ConnectionString}", ConnectionString);
        }
    }

    public interface ILogRepository
    {
        System.Threading.Tasks.Task AddAsync(LogEntry logEntry, System.Threading.CancellationToken cancellationToken = default);

        System.Threading.Tasks.Task<IReadOnlyList<LogEntry>> GetRecentAsync(int currentPage, int pageSize, System.Threading.CancellationToken cancellationToken = default);

        System.Threading.Tasks.Task<IReadOnlyList<LogEntry>> SearchAsync(LogSqlQuery query, int currentPage, int pageSize, System.Threading.CancellationToken cancellationToken = default);
    }

    public class SqliteLogRepository : ILogRepository
    {
        private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly AppLogsContext _context;

        public SqliteLogRepository(AppLogsContext context)
        {
            _context = context;
        }

        public async System.Threading.Tasks.Task AddAsync(LogEntry logEntry, System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(logEntry.Id))
            {
                logEntry.Id = Guid.NewGuid().ToString("n");
            }

            if (logEntry.Timestamp == default)
            {
                logEntry.Timestamp = DateTimeOffset.UtcNow;
            }

            using var connection = _context.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO LogEntries (Id, TimestampUtc, Level, RenderedMessage, Exception, PropertiesJson)
VALUES ($id, $timestampUtc, $level, $renderedMessage, $exception, $propertiesJson);";

            command.Parameters.AddWithValue("$id", logEntry.Id);
            command.Parameters.AddWithValue("$timestampUtc", logEntry.Timestamp.ToUniversalTime().ToString("O"));
            command.Parameters.AddWithValue("$level", logEntry.Level ?? string.Empty);
            command.Parameters.AddWithValue("$renderedMessage", logEntry.RenderedMessage ?? string.Empty);
            command.Parameters.AddWithValue("$exception", (object)logEntry.Exception ?? DBNull.Value);
            command.Parameters.AddWithValue("$propertiesJson", System.Text.Json.JsonSerializer.Serialize(logEntry.Properties ?? new Dictionary<string, object>(), JsonOptions));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async System.Threading.Tasks.Task<IReadOnlyList<LogEntry>> GetRecentAsync(int currentPage, int pageSize, System.Threading.CancellationToken cancellationToken = default)
        {
            return await SearchAsync(new LogSqlQuery("1 = 1", Array.Empty<LogSqlParameter>()), currentPage, pageSize, cancellationToken);
        }

        public async System.Threading.Tasks.Task<IReadOnlyList<LogEntry>> SearchAsync(LogSqlQuery query, int currentPage, int pageSize, System.Threading.CancellationToken cancellationToken = default)
        {
            var safePage = Math.Max(currentPage, 1);
            var safePageSize = Math.Clamp(pageSize, 1, 500);
            var offset = (safePage - 1) * safePageSize;

            using var connection = _context.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = $@"
SELECT Id, TimestampUtc, Level, RenderedMessage, Exception, PropertiesJson
FROM LogEntries
WHERE {query.WhereSql}
ORDER BY TimestampUtc DESC
LIMIT $limit OFFSET $offset;";

            foreach (var parameter in query.Parameters)
            {
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
            }

            command.Parameters.AddWithValue("$limit", safePageSize);
            command.Parameters.AddWithValue("$offset", offset);

            var logEntries = new List<LogEntry>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                logEntries.Add(ReadLogEntry(reader));
            }

            return logEntries;
        }

        internal static LogEntry ReadLogEntry(SqliteDataReader reader)
        {
            var propertiesJson = reader.GetString(5);
            var properties = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(propertiesJson, JsonOptions)
                ?? new Dictionary<string, object>();

            return new LogEntry
            {
                Id = reader.GetString(0),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture),
                Level = reader.GetString(2),
                RenderedMessage = reader.GetString(3),
                Exception = reader.IsDBNull(4) ? null : reader.GetString(4),
                Properties = properties
            };
        }
    }
}
