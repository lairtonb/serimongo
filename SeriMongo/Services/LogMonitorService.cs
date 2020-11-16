using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using SeriMongo.Data;
using SeriMongo.Hubs;
using SeriMongo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SeriMongo.Services
{
    public class LogMonitorService : BackgroundService
    {
        private readonly IHubContext<LoggingHub>  _loggingHub;
        private readonly AppLogsContext _logsContext;
        private readonly ILogger<LogMonitorService> _logger;

        public LogMonitorService(ILogger<LogMonitorService> logger,
            AppLogsContext logsContext,
            IHubContext<LoggingHub> hubContext)
        {
            _loggingHub = hubContext;
            _logsContext = logsContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await StartMonitoring();
        }

        private Task StartMonitoring()
        {
            // Reference:
            // https://mongodb.github.io/mongo-csharp-driver/2.10/reference/driver/change_streams/#watching-changes-in-a-single-collection

            return Task.Run(async () => 
            {
                try
                {
                    var collection = _logsContext.LogEtries;

                    // We will use this to monitor the logs as they are being inserted into MongoDB
                    ChangeStreamOptions opt = new ChangeStreamOptions()
                    {
                        FullDocument = ChangeStreamFullDocumentOption.UpdateLookup
                    };

                    // The operationType can be one of the following: 
                    // insert, update, replace, delete, invalidate
                    var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<LogEntry>>().Match("{ operationType: { $in: [ 'insert' ] } }");

                    var changeStream = collection.Watch(pipeline, opt).ToEnumerable().GetEnumerator();

                    while (changeStream.MoveNext())
                    {
                        var next = changeStream.Current;
                        await _loggingHub.Clients.All.SendAsync("OnReceiveLogEntry", next.FullDocument);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }
            });
        }
    }
}
