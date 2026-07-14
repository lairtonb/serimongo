using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SeriMongo.Data
{
    public sealed class SqliteSchemaMigrator
    {
        private static readonly IReadOnlyList<Migration> Migrations = new[]
        {
            new Migration(1, @"
CREATE TABLE IF NOT EXISTS LogEntries (
    Id TEXT NOT NULL PRIMARY KEY,
    TimestampUtc TEXT NOT NULL,
    Level TEXT NOT NULL,
    RenderedMessage TEXT NOT NULL,
    Exception TEXT NULL,
    PropertiesJson TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_LogEntries_TimestampUtc ON LogEntries (TimestampUtc DESC);
CREATE INDEX IF NOT EXISTS IX_LogEntries_Level ON LogEntries (Level);"),
            new Migration(2, @"
CREATE TABLE IF NOT EXISTS TraceSummaries (
    TraceId TEXT NOT NULL PRIMARY KEY,
    StartTimestampUtc TEXT NOT NULL,
    EndTimestampUtc TEXT NOT NULL,
    DurationNanoseconds INTEGER NOT NULL,
    RootSpanId TEXT NULL,
    RootSpanName TEXT NOT NULL,
    RootServiceName TEXT NOT NULL,
    SpanCount INTEGER NOT NULL,
    ErrorSpanCount INTEGER NOT NULL,
    ServiceNamesJson TEXT NOT NULL,
    LastUpdatedUtc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_TraceSummaries_StartTimestampUtc ON TraceSummaries (StartTimestampUtc DESC);
CREATE INDEX IF NOT EXISTS IX_TraceSummaries_RootServiceName_StartTimestampUtc ON TraceSummaries (RootServiceName, StartTimestampUtc DESC);
CREATE INDEX IF NOT EXISTS IX_TraceSummaries_ErrorSpanCount_StartTimestampUtc ON TraceSummaries (ErrorSpanCount, StartTimestampUtc DESC);
CREATE INDEX IF NOT EXISTS IX_TraceSummaries_DurationNanoseconds_StartTimestampUtc ON TraceSummaries (DurationNanoseconds, StartTimestampUtc DESC);

CREATE TABLE IF NOT EXISTS TraceSpans (
    TraceId TEXT NOT NULL,
    SpanId TEXT NOT NULL,
    ParentSpanId TEXT NULL,
    TraceState TEXT NOT NULL,
    Flags INTEGER NOT NULL,
    Name TEXT NOT NULL,
    Kind INTEGER NOT NULL,
    StartTimestampUtc TEXT NOT NULL,
    EndTimestampUtc TEXT NOT NULL,
    DurationNanoseconds INTEGER NOT NULL,
    StatusCode INTEGER NOT NULL,
    StatusMessage TEXT NULL,
    ServiceName TEXT NOT NULL,
    ResourceAttributesJson TEXT NOT NULL,
    ScopeName TEXT NULL,
    ScopeVersion TEXT NULL,
    AttributesJson TEXT NOT NULL,
    EventsJson TEXT NOT NULL,
    LinksJson TEXT NOT NULL,
    DroppedAttributesCount INTEGER NOT NULL,
    DroppedEventsCount INTEGER NOT NULL,
    DroppedLinksCount INTEGER NOT NULL,
    PRIMARY KEY (TraceId, SpanId)
);

CREATE INDEX IF NOT EXISTS IX_TraceSpans_TraceId_StartTimestampUtc ON TraceSpans (TraceId, StartTimestampUtc);
CREATE INDEX IF NOT EXISTS IX_TraceSpans_ParentSpanId ON TraceSpans (ParentSpanId);
CREATE INDEX IF NOT EXISTS IX_TraceSpans_ServiceName_StartTimestampUtc ON TraceSpans (ServiceName, StartTimestampUtc DESC);
CREATE INDEX IF NOT EXISTS IX_TraceSpans_StatusCode_StartTimestampUtc ON TraceSpans (StatusCode, StartTimestampUtc DESC);")
        };

        private readonly ILogger<SqliteSchemaMigrator> _logger;

        public SqliteSchemaMigrator(ILogger<SqliteSchemaMigrator> logger)
        {
            _logger = logger;
        }

        public void Migrate(string connectionString)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            EnsureMigrationTable(connection, transaction);

            var appliedVersions = GetAppliedVersions(connection, transaction);
            foreach (var migration in Migrations.Where(migration => !appliedVersions.Contains(migration.Version)))
            {
                ExecuteMigration(connection, transaction, migration);
                RecordMigration(connection, transaction, migration.Version);
                _logger.LogInformation("Applied SQLite schema migration {MigrationVersion}", migration.Version);
            }

            transaction.Commit();
        }

        private static void EnsureMigrationTable(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS SchemaMigrations (
    Version INTEGER NOT NULL PRIMARY KEY,
    AppliedUtc TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }

        private static HashSet<int> GetAppliedVersions(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT Version FROM SchemaMigrations;";

            using var reader = command.ExecuteReader();
            var appliedVersions = new HashSet<int>();
            while (reader.Read())
            {
                appliedVersions.Add(reader.GetInt32(0));
            }

            return appliedVersions;
        }

        private static void ExecuteMigration(SqliteConnection connection, SqliteTransaction transaction, Migration migration)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = migration.Sql;
            command.ExecuteNonQuery();
        }

        private static void RecordMigration(SqliteConnection connection, SqliteTransaction transaction, int version)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO SchemaMigrations (Version, AppliedUtc)
VALUES ($version, $appliedUtc);";
            command.Parameters.AddWithValue("$version", version);
            command.Parameters.AddWithValue("$appliedUtc", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }

        private sealed class Migration
        {
            public Migration(int version, string sql)
            {
                Version = version;
                Sql = sql;
            }

            public int Version { get; }

            public string Sql { get; }
        }
    }
}
