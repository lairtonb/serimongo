using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.AspNet.OData.Formatter.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OpenApi.Models;
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

            // This is used by the LogMonitorService to propagate logs to clients
            services.AddSignalR(configure => { 
                // Can fine-tune settings
                // configure.
            });

            services.AddOData();

            // https://github.com/hassanhabib/OData3.1WithSwagger/blob/master/WeatherAPI2/Startup.cs
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "SeriMongo", Version = "v1" });
            });

            // Required by OData
            SetOutputFormatters(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseCors("CorsPolicy");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                /* Api */
                endpoints.MapControllers();

                /* Odata */

                var odataBuilder = new ODataConventionModelBuilder();
                EntitySetConfiguration<LogEntry> logEntry = odataBuilder.EntitySet<LogEntry>("AppLogs");

                ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
                var customSave = builder.EntityType<LogEntry>().Collection.Action("Properties");
                customSave.ReturnsCollection<Dictionary<string, object>>();

                endpoints.Filter().OrderBy().MaxTop(10).Count();
                endpoints.MapODataRoute("odata", "odata", odataBuilder.GetEdmModel());

                // Uncomment the following line to Work-around for #1175 in beta1
                endpoints.EnableDependencyInjection();

                /* SignalR */
                endpoints.MapHub<LoggingHub>("/logs");
            });

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SeriMongo API V1");
            });
        }

        private static void SetOutputFormatters(IServiceCollection services)
        {
            services.AddMvcCore(options =>
            {
                IEnumerable<ODataOutputFormatter> outputFormatters =
                    options.OutputFormatters.OfType<ODataOutputFormatter>()
                        .Where(foramtter => foramtter.SupportedMediaTypes.Count == 0);

                foreach (var outputFormatter in outputFormatters)
                {
                    outputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/odata"));
                }
            });
        }

    }



}
