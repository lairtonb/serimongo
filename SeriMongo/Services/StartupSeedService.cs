using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeriMongo.Data;
using SeriMongo.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Services
{
    public class StartupSeedService
    {
        private readonly AppLogsContext _context;
        private readonly ApplicationOptions _options;
        private readonly ILogger<StartupSeedService> _logger;

        public StartupSeedService(
            AppLogsContext context,
            IOptions<ApplicationOptions> options,
            ILogger<StartupSeedService> logger)
        {
            _context = context;
            _options = options.Value;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.Seed.Enabled)
            {
                _logger.LogInformation("Startup seed is disabled.");
                return;
            }

            var scriptPath = _options.Seed.ScriptPath;
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
            {
                _logger.LogWarning("Startup seed was enabled but seed script was not found at {ScriptPath}.", scriptPath);
                return;
            }

            var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(sql))
            {
                _logger.LogWarning("Startup seed script at {ScriptPath} is empty.", scriptPath);
                return;
            }

            using var connection = _context.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Startup seed script {ScriptPath} executed. Affected rows: {AffectedRows}.", scriptPath, affectedRows);
        }
    }
}
