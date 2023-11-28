using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Compact;

namespace SerilogSample
{
    public class Program
    {
        static readonly LoggerProviderCollection Providers = new LoggerProviderCollection();

        public static void Main(string[] args)
        {
            /*
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                // .WriteTo.Console(new RenderedCompactJsonFormatter())
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
                )
                .WriteTo.File(new Serilog.Formatting.Json.JsonFormatter(), "C:\\Projects_DotNet_Angular\\logs.json")
                .WriteTo.Providers(Providers)
                .CreateLogger();

            try
            {
                Log.Information("Starting up");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application start-up failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
            */

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var levelSwitch = new LoggingLevelSwitch(initialMinimumLevel: LogEventLevel.Information);

            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, configurationBuilder) =>
                {
                })
                .ConfigureServices((hostingContext, serviceCollection) =>
                {
                    // https://nblumhardt.com/2014/10/dynamically-changing-the-serilog-level/
                    serviceCollection.AddSingleton(levelSwitch);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .UseSerilog((hostingContext, loggerConfiguration) =>
                {
                    var cons = hostingContext.Configuration.GetConnectionString("BaseLog");
                    loggerConfiguration
                        .ReadFrom.Configuration(hostingContext.Configuration)

                        // You can use a controller to change this dynamically
                        .MinimumLevel.ControlledBy(levelSwitch)

                        // This is recomended because in the Startup.Configure we can add UseSerilogRequestLogging
                        // which is less verbose and provides just enough information (but is flexible to add more info)
                        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)

                        // This can be set in the configuration
                        .Enrich.FromLogContext()

                        // Our main sink
                        .WriteTo.MongoDB(cons, collectionName: "ApplicationLogs")

                        // When running in VMs, we could also log to file as a fallback
                        // It would be a good idea to use a rolling file sink
                        .WriteTo.File(@"Logs\Serilog.Sinks.MongoDB.Sample.log")

                        // In Cloud Foundry (or other clouds) it may yield better results to write to
                        // console in json format:
                        // .WriteTo.Console(new RenderedCompactJsonFormatter())
                        .WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                            levelSwitch: levelSwitch
                        );
                }, false,
                // If you want to also keep .NET Core native providers,
                // then in this case set writeToProviders: true
                writeToProviders: false);
        }
    }
}
