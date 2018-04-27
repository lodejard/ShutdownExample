using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WebAppShutdown
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IStartupFilter, ControlShutdown>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog(
                    new LoggerConfiguration()
                        .Enrich.FromLogContext()
                        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {RequestPath}{Scope} {Message}{NewLine}{Exception}")
                        .CreateLogger(),
                    dispose: true);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            IApplicationLifetime applicationLifetime,
            ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Map("/stop", map => map.Run(async (context) =>
            {
                using (logger.BeginScope("StopRequest"))
                {
                    logger.LogInformation("Before StopApplication");
                    applicationLifetime.StopApplication();
                    logger.LogInformation("After StopApplication");
                }
            }));

            app.Run(async (context) =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(
                    @"<html>
                        <head><title>WebAppShutdown</title></head>
                        <body>
                            <p><a href=""/stop"">Stop</a></p>
                        </body>
                    </html>");
            });
        }
    }
}
