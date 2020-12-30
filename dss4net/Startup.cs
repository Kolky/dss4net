using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.Extensions.Logging.Console;
using System;
using System.IO;
using System.Linq;
using System.Net;

namespace dss4net
{
    public class Startup
    {
        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.Configure<ConsoleLoggerOptions>(
                config =>
                {
                    config.TimestampFormat = "[HH:mm:ss.fff] ";
                });
            services.Configure<AzureFileLoggerOptions>(options =>
            {
                options.FileName = "azure-diagnostics-";
                options.FileSizeLimit = 50 * 1024;
                options.RetainedFileCountLimit = 5;
            });
            services.Configure<AzureBlobLoggerOptions>(options =>
            {
                options.BlobName = "azure-log.txt";
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IMemoryCache cache, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/echo/{data}", async context =>
                {
                    var data = context.Request.RouteValues["data"].ToString();
                    await context.Response.WriteAsync(data);
                });

                endpoints.MapPost("/data/{id}", async context =>
                {
                    var id = context.Request.RouteValues["id"].ToString();

                    if (!cache.TryGetValue(id, out FixedSizeQueue<(byte[], string)> queue))
                    {
                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromMinutes(this.configuration.GetValue("slidingExpiration", 5.0)));

                        queue = new FixedSizeQueue<(byte[], string)>(this.configuration.GetValue("fixedSize", 50));
                        cache.Set(id, queue, cacheEntryOptions);
                    }

                    byte[] body;
                    using (var stream = new MemoryStream())
                    {
                        await context.Request.Body.CopyToAsync(stream);
                        body = stream.ToArray();
                    }

                    queue.Enqueue((body, context.Request.ContentType));

                    logger.LogInformation($"Post '{id}': {body.Length} ({context.Request.ContentType})");

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await context.Response.Body.FlushAsync();
                });

                endpoints.MapGet("/data/{id}", async context =>
                {
                    var id = context.Request.RouteValues["id"].ToString();
                    if (!cache.TryGetValue(id, out FixedSizeQueue<(byte[], string)> queue))
                    {
                        logger.LogInformation($"Get '{id}' does not exist");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                    else if (!queue.Any())
                    {
                        logger.LogInformation($"Get '{id}' has no data");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                    else if (queue.TryDequeue(out (byte[] body, string contentType) result))
                    {
                        logger.LogInformation($"Get '{id}': {result.body.Length} ({result.contentType})");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentType = result.contentType;
                        await context.Response.Body.WriteAsync(result.body);
                    }

                    await context.Response.Body.FlushAsync();
                });
            });
        }
    }
}
