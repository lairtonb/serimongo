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

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/info", (IOptions<SimulatorOptions> options) => new
{
    targetBaseUrl = options.Value.TargetBaseUrl
});

app.MapPost("/api/logs/{level}", async (
    string level,
    LogScenarioFactory scenarioFactory,
    LogEmitterClient emitter,
    CancellationToken cancellationToken) =>
{
    if (!scenarioFactory.TryCreate(level, out var log))
    {
        return Results.BadRequest(new
        {
            error = $"Unsupported level '{level}'."
        });
    }

    await emitter.EmitAsync([log], cancellationToken);

    return Results.Ok(new
    {
        level = log.Level,
        count = 1,
        message = log.Message
    });
});

app.MapPost("/api/logs/burst", async (
    BurstRequest request,
    LogScenarioFactory scenarioFactory,
    LogEmitterClient emitter,
    CancellationToken cancellationToken) =>
{
    var count = Math.Clamp(request.Count <= 0 ? 25 : request.Count, 1, 200);
    var logs = scenarioFactory.CreateBurst(count);

    await emitter.EmitAsync(logs, cancellationToken);

    return Results.Ok(new
    {
        count,
        message = $"Emitted {count} simulated logs."
    });
});

app.MapFallbackToFile("index.html");

app.Run();

internal sealed record BurstRequest(int Count);
