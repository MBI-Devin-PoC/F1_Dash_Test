using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Live.Models;
using Live.Services;
using Live.Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("F1");
builder.Services.AddSingleton<F1Client>();
builder.Services.AddSingleton<StateManager>();

builder.Services.AddCors(options =>
{
    var origins = Environment.GetEnvironmentVariable("ORIGIN")?.Split(';') ?? ["http://localhost:3000"];
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .WithMethods("GET", "CONNECT");
    });
});

var app = builder.Build();

app.UseCors();

var stateManager = app.Services.GetRequiredService<StateManager>();
stateManager.Start();

app.MapGet("/api/health", () => Results.Ok(new { success = true }));

app.MapGet("/api/drivers", (StateManager stateManager) =>
{
    var driverList = stateManager.GetDriverList();
    if (driverList is JsonObject obj)
    {
        var drivers = obj.Select(kvp => kvp.Value).ToList();
        return Results.Ok(drivers);
    }
    else if (driverList is JsonArray arr)
    {
        return Results.Ok(arr);
    }
    return Results.StatusCode(500);
});

app.MapGet("/api/sse", async (HttpContext context, StateManager stateManager, ILogger<Program> logger) =>
{
    var acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
    var useGzip = acceptEncoding.Contains("gzip");

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";
    context.Response.Headers["X-Accel-Buffering"] = "no";

    if (useGzip)
    {
        context.Response.Headers.ContentEncoding = "gzip";
        context.Response.Headers.Vary = "Accept-Encoding";
    }

    var (initialState, updatesChannel) = stateManager.Subscribe();

    try
    {
        Stream outputStream = context.Response.Body;
        GZipStream? gzipStream = null;

        if (useGzip)
        {
            gzipStream = new GZipStream(outputStream, CompressionLevel.Fastest, leaveOpen: true);
            outputStream = gzipStream;
        }

        await WriteSseEventAsync(outputStream, "initial", initialState, useGzip);

        var keepAliveTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        var keepAliveTask = Task.Run(async () =>
        {
            while (await keepAliveTimer.WaitForNextTickAsync(context.RequestAborted))
            {
                try
                {
                    var keepAliveBytes = Encoding.UTF8.GetBytes("data: keep-alive-text\n\n");
                    await outputStream.WriteAsync(keepAliveBytes, context.RequestAborted);
                    await outputStream.FlushAsync(context.RequestAborted);
                    if (gzipStream is not null)
                    {
                        await gzipStream.FlushAsync(context.RequestAborted);
                    }
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        await foreach (var message in updatesChannel.Reader.ReadAllAsync(context.RequestAborted))
        {
            var (eventName, data) = message switch
            {
                Message.Initial initial => ("initial", initial.Data),
                Message.Updates updates => ("update", BatchUpdates(updates.Items)),
                _ => (null, null)
            };

            if (eventName is not null && data is not null)
            {
                await WriteSseEventAsync(outputStream, eventName, data, useGzip);
                if (gzipStream is not null)
                {
                    await gzipStream.FlushAsync(context.RequestAborted);
                }
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
        }

        keepAliveTimer.Dispose();
        await keepAliveTask;

        if (gzipStream is not null)
        {
            await gzipStream.DisposeAsync();
        }
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("SSE connection closed by client");
    }
    finally
    {
        stateManager.Unsubscribe(updatesChannel);
    }
});

var address = Environment.GetEnvironmentVariable("LIVE_ADDRESS") ?? "0.0.0.0:4000";
var parts = address.Split(':');
var host = parts[0];
var port = int.Parse(parts.Length > 1 ? parts[1] : "4000");

app.Urls.Clear();
app.Urls.Add($"http://{host}:{port}");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting live service on {Address}", address);

app.Run();

static JsonObject BatchUpdates(List<(string Topic, JsonNode Data)> updates)
{
    var batched = new JsonObject();
    foreach (var (topic, data) in updates)
    {
        JsonMerge.MergeInto(batched, topic, data);
    }
    return batched;
}

static async Task WriteSseEventAsync(Stream stream, string eventName, JsonNode data, bool isGzip)
{
    var json = data.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    var eventText = $"event: {eventName}\ndata: {json}\n\n";
    var bytes = Encoding.UTF8.GetBytes(eventText);
    await stream.WriteAsync(bytes);
    await stream.FlushAsync();
}
