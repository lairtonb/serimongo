using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeriMongo.Data;
using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SeriMongo.Tests.Data
{
    public sealed class SqliteSchemaMigratorTests : IDisposable
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"serimongo-tests-{Guid.NewGuid():N}.db");

        private string ConnectionString => $"Data Source={_databasePath};Pooling=False";

        [Fact]
        public async Task AppLogsContext_UpgradesLogsOnlyDatabaseWithoutLosingEntries()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await CreateLogsOnlyDatabaseAsync(cancellationToken);

            _ = new AppLogsContext(
                Options.Create(new ApplicationOptions
                {
                    Database = new DatabaseSettings
                    {
                        ConnectionString = ConnectionString
                    }
                }),
                new SqliteSchemaMigrator(NullLogger<SqliteSchemaMigrator>.Instance),
                NullLogger<AppLogsContext>.Instance);

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            Assert.Equal("Legacy entry", await ReadSingleStringAsync(connection, "SELECT RenderedMessage FROM LogEntries WHERE Id = 'legacy-entry';", cancellationToken));
            Assert.True(await TableExistsAsync(connection, "TraceSummaries", cancellationToken));
            Assert.True(await TableExistsAsync(connection, "TraceSpans", cancellationToken));
            Assert.Equal(new[] { 1, 2 }, await ReadMigrationVersionsAsync(connection, cancellationToken));
        }

        public void Dispose()
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }

        private async Task CreateLogsOnlyDatabaseAsync(CancellationToken cancellationToken)
        {
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE LogEntries (
    Id TEXT NOT NULL PRIMARY KEY,
    TimestampUtc TEXT NOT NULL,
    Level TEXT NOT NULL,
    RenderedMessage TEXT NOT NULL,
    Exception TEXT NULL,
    PropertiesJson TEXT NOT NULL
);

INSERT INTO LogEntries (Id, TimestampUtc, Level, RenderedMessage, Exception, PropertiesJson)
VALUES ('legacy-entry', '2026-07-13T12:00:00.0000000+00:00', 'Information', 'Legacy entry', NULL, '{}');";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<string> ReadSingleStringAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (string)await command.ExecuteScalarAsync(cancellationToken);
        }

        private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT EXISTS(
    SELECT 1
    FROM sqlite_master
    WHERE type = 'table' AND name = $tableName
);";
            command.Parameters.AddWithValue("$tableName", tableName);
            return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 1;
        }

        private static async Task<IReadOnlyList<int>> ReadMigrationVersionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Version FROM SchemaMigrations ORDER BY Version;";

            var versions = new List<int>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                versions.Add(reader.GetInt32(0));
            }

            return versions;
        }
    }
}
