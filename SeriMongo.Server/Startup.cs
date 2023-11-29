using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using MongoDB.Bson.Serialization;
using SeriMongo.Data;
using SeriMongo.Hubs;
using SeriMongo.Models;
using SeriMongo.Services;

namespace SeriMongo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();

            // Add our Config object so it can be injected
            // services.Configure<ApplicationOptions>(Configuration.GetSection("ApplicationOptions"));
            services.Configure<ApplicationOptions>(options => Configuration.GetSection("ApplicationOptions").Bind(options));

            services.AddControllers();

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder => builder
                    .WithOrigins("http://localhost:4200")
                    // .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .SetIsOriginAllowed(_ => true)
                    // .AllowAnyOrigin and .AllowCredentials not allowed in the same method
                    .AllowCredentials()
                );
            });

            // Register the class that uses MongoDB ChangeStreams to listen to logs
            services.AddHostedService<LogMonitorService>();

            // MongoDB Data Access
            services.AddSingleton<AppLogsContext>();

            // MongoDB Search using JSON
            if (!BsonClassMap.IsClassMapRegistered(typeof(LogEntry)))
            {
                BsonClassMap.RegisterClassMap<LogEntry>();
            }

            // This is used by the LogMonitorService to propagate logs to clients
            services.AddSignalR(configure =>
            {
                // Can fine-tune settings
                // configure.
            });

            // https://github.com/hassanhabib/OData3.1WithSwagger/blob/master/WeatherAPI2/Startup.cs
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "SeriMongo", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseCors("CorsPolicy");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                /* Api */
                endpoints.MapControllers();

                /* SignalR */
                endpoints.MapHub<LoggingHub>("/logs");
            });

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SeriMongo API V1");
            });
        }
    }



}
