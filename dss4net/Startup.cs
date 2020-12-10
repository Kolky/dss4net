using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace dss4net
{
    public class Startup
    {
        private readonly IDictionary<string, Queue<(byte[], string)>> datastore = new Dictionary<string, Queue<(byte[], string)>>();

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/data/{id}", async context =>
                {
                    var id = context.Request.RouteValues["id"].ToString();
                    if (!this.datastore.ContainsKey(id))
                    {
                        this.datastore.Add(id, new Queue<(byte[], string)>());
                    }

                    byte[] body;
                    using (var stream = new MemoryStream())
                    {
                        context.Request.Body.CopyTo(stream);
                        body = stream.ToArray();
                    }

                    logger.LogInformation($"Recieved '{id}': {body.Length} ({context.Request.ContentType})");

                    this.datastore[id].Enqueue((body, context.Request.ContentType));

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await context.Response.Body.FlushAsync();
                });

                endpoints.MapGet("/data/{id}", async context =>
                {
                    var id = context.Request.RouteValues["id"].ToString();
                    if (!this.datastore.ContainsKey(id))
                    {
                        logger.LogInformation($"Sent '{id}' does not exist");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                    else if (!this.datastore[id].Any())
                    {
                        logger.LogInformation($"Sent '{id}' has no data");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                    else
                    {
                        (byte[] body, string contentType) = this.datastore[id].Dequeue();

                        logger.LogInformation($"Sent '{id}': {body.Length} ({contentType})");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentType = contentType;
                        await context.Response.Body.WriteAsync(body);
                    }

                    await context.Response.Body.FlushAsync();
                });
            });
        }
    }
}
