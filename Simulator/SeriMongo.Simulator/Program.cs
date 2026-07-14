using Microsoft.Extensions.Options;
using SeriMongo.Simulator;
using SeriMongo.Simulator.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "dist/SeriMongoSimulator"
});

builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection(SimulatorOptions.SectionName));
builder.Services.AddSingleton<LogScenarioFactory>();
builder.Services.AddHttpClient<LogEmitterClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SimulatorOptions>>().Value;
    client.BaseAddress = options.GetTargetBaseUri();
});
builder.Services.AddHttpClient<TraceEmitterClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SimulatorOptions>>().Value;
    client.BaseAddress = options.GetTargetBaseUri();
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/info", (IOptions<SimulatorOptions> options) => new
{
    targetBaseUrl = options.Value.TargetBaseUrl,
    serviceNames = LogScenarioFactory.ServiceNames
});

app.MapPost("/api/logs/{level}", async (
    string level,
    LogScenarioFactory scenarioFactory,
    LogEmitterClient emitter,
    TraceEmitterClient traceEmitter,
    CancellationToken cancellationToken) =>
{
    if (!scenarioFactory.TryCreateOperation(level, out var operation))
    {
        return Results.BadRequest(new
        {
            error = $"Unsupported level '{level}'."
        });
    }

    await traceEmitter.EmitAsync(operation.Spans, cancellationToken);
    await emitter.EmitAsync(operation.Logs, cancellationToken);

    return Results.Ok(new
    {
        level,
        serviceName = operation.Logs[0].ServiceName,
        count = operation.Logs.Count,
        spanCount = operation.Spans.Count,
        message = $"Emitted {operation.Logs.Count} correlated {level} log and {operation.Spans.Count} spans."
    });
});

app.MapPost("/api/logs/burst", async (
    BurstRequest request,
    LogScenarioFactory scenarioFactory,
    LogEmitterClient emitter,
    TraceEmitterClient traceEmitter,
    CancellationToken cancellationToken) =>
{
    var count = Math.Clamp(request.Count <= 0 ? 25 : request.Count, 1, 200);
    var operations = scenarioFactory.CreateBurst(count);
    var logs = operations.SelectMany(operation => operation.Logs).ToArray();
    var spans = operations.SelectMany(operation => operation.Spans).ToArray();

    await traceEmitter.EmitAsync(spans, cancellationToken);
    await emitter.EmitAsync(logs, cancellationToken);

    return Results.Ok(new
    {
        count = logs.Length,
        spanCount = spans.Length,
        message = $"Emitted {logs.Length} correlated logs and {spans.Length} spans across {LogScenarioFactory.ServiceNames.Length} services."
    });
});

app.MapFallbackToFile("index.html");

app.Run();

internal sealed record BurstRequest(int Count);
