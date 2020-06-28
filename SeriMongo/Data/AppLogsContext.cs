using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SeriMongo.Data
{
    public class AppLogsContext
    {
        private readonly IMongoDatabase _database = null;
        private readonly ApplicationOptions _applicationOptions;

        public AppLogsContext(IOptions<ApplicationOptions> applicationOptions)
        {
            _applicationOptions = applicationOptions.Value;
            var client = new MongoClient(_applicationOptions.ConnectionInfo.ConnectionString);
            if (client != null)
                _database = client.GetDatabase(_applicationOptions.ConnectionInfo.DatabaseName);
        }

        public IMongoCollection<LogEntry> LogEtries
        {
            get
            {
                return _database.GetCollection<LogEntry>(_applicationOptions.ConnectionInfo.CollectionName);
            }
        }
    }
}
