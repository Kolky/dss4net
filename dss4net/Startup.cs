using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
        private readonly IDictionary<string, Queue<string>> datastore = new Dictionary<string, Queue<string>>();

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
                        this.datastore.Add(id, new Queue<string>());
                    }

                    string body;
                    using (var stream = new StreamReader(context.Request.Body))
                    {
                        body = stream.ReadToEnd();
                    }

                    logger.LogInformation($"Recieved '{id}': {body} ({context.Request.ContentType})");

                    this.datastore[id].Enqueue(body);

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
                        var body = this.datastore[id].Dequeue();

                        logger.LogInformation($"Sent '{id}': {body}");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        await context.Response.WriteAsync(body);
                    }

                    await context.Response.Body.FlushAsync();
                });
            });
        }
    }
}
