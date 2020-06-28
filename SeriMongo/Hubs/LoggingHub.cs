using Microsoft.AspNetCore.SignalR;
using SeriMongo.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SeriMongo.Hubs
{
    public class LoggingHub: Hub
    {
        public async Task SendLogEntry(LogEntry logEntry)
        {
            await Clients.All.SendAsync("OnReceiveLogEntry", logEntry);
        }
    }



}