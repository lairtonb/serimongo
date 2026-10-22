# Getting Started

This guide takes you from a fresh clone to logs from your own application
showing up in SeriMongo.

## What SeriMongo Is

SeriMongo is a log and trace viewer. Your applications send logs and traces
to it over HTTP, it stores them in a SQLite file, and its web UI shows them in
real time.

Despite the name, SeriMongo no longer uses MongoDB. Older versions stored logs
written by `Serilog.Sinks.MongoDB` and needed a MongoDB replica set for change
streams. If you see `The $changeStream stage is only supported on replica sets`,
you are running an old version.

The repository contains two applications:

| Project | What it is |
| :-- | :-- |
| `SeriMongo/SeriMongo.csproj` | The log viewer. An ASP.NET Core API that receives and stores logs, plus the Angular UI in the same folder. In Docker, and after an Angular build, the API also serves the UI. |
| `Simulator/SeriMongo.Simulator/SeriMongo.Simulator.csproj` | Optional. A small app with buttons that send sample logs to SeriMongo, so you can try it without wiring up your own application. |

## Option 1: Run With Docker (Recommended)

You only need [Docker](https://docs.docker.com/get-docker/). .NET and Node.js
are not required, because the images build everything.

From the repository root:

```bash
docker compose up --build
```

The first build downloads the .NET SDK and Node.js images and takes a few
minutes. When it finishes:

| URL | What you get |
| :-- | :-- |
| <http://localhost:51983> | SeriMongo UI, pre-filled with 3000 sample logs |
| <http://localhost:51984> | Simulator. Click a level button to send a log, then watch it arrive in the UI. |
| <http://localhost:51983/swagger> | API reference |

Logs are stored in the `serimongo-data` Docker volume and survive restarts.
To start over with an empty database:

```bash
docker compose down -v
```

## Option 2: Run From Source

Use this option when you want to change SeriMongo itself.

Install:

* the [.NET 10 SDK](https://dotnet.microsoft.com/download); and
* [Node.js](https://nodejs.org/) 22.22.3+ or 24.15.0+ (needed by Angular 22),
  which includes npm.

The API and the UI are separate processes. Pick one of the two workflows below.

### Single process: API serves a built UI

```bash
cd SeriMongo
npm ci
npm run build
dotnet run --urls http://localhost:51983
```

Open <http://localhost:51983>. Run `npm run build` again after UI changes.

If you skip the Angular build, the API still starts and
<http://localhost:51983/swagger> works, but <http://localhost:51983> returns
404 because there is no UI to serve.

### Two processes: Angular dev server with live reload

Terminal 1, the API:

```bash
dotnet run --project SeriMongo/SeriMongo.csproj --urls http://localhost:51983
```

Terminal 2, the UI:

```bash
cd SeriMongo
npm ci
npm start
```

Open <http://localhost:4200>. The dev server forwards `/api`, `/logs`
(SignalR), `/v1` and `/otlp` to the API on port 51983 through
`SeriMongo/proxy.config.json`, so keep the API on that port. Port 4200 only
answers while `npm start` is running.

In Visual Studio, open `SeriMongo.sln`, run the `SeriMongo` profile for the
API, and run `npm start` from a terminal for the UI.

When run from source, logs are stored in `SeriMongo/Data/serimongo.db`. Seed
data is off unless you enable it (see [Configuration](#configuration)).

## Send Logs From Your Application

SeriMongo accepts:

* **OTLP/HTTP** logs at `/v1/logs` (and `/otlp/v1/logs`) and traces at
  `/v1/traces` (and `/otlp/v1/traces`), in either Protobuf or JSON. This is
  the OpenTelemetry standard. Gzip-compressed requests are accepted.
* **SeriMongo JSON** at `/api/applogs`, for scripts or quick tests.

OTLP/gRPC is not supported. Use OTLP/HTTP, or put an OpenTelemetry Collector
in between, as described below.

### Quick test with curl

```bash
curl -X POST http://localhost:51983/api/applogs   -H "Content-Type: application/json"   -d '{ "level": "Warning", "renderedMessage": "hello from curl", "properties": { "OrderId": 42 } }'
```

### .NET and Serilog

With Serilog, add the `Serilog.Sinks.OpenTelemetry` package:

```csharp
using Serilog;
using Serilog.Sinks.OpenTelemetry;

Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = "http://localhost:51983/v1/logs";
        options.Protocol = OtlpProtocol.HttpProtobuf;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "my-app"
        };
    })
    .CreateLogger();

Log.Warning("Order {OrderId} failed", 42);
```

`OrderId` appears in SeriMongo as a structured property, so you can search
for it with `prop.OrderId = 42`.

With the OpenTelemetry .NET SDK, keep the default OTLP exporter and switch it
from gRPC to HTTP:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:51983
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

### Other OpenTelemetry exporters

Point the exporter at SeriMongo and pick an HTTP protocol:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:51983
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

`http/json` works too.

### Exporters that only speak gRPC

The repository includes a Collector setup that accepts OTLP/gRPC and
OTLP/HTTP and forwards logs to SeriMongo. Start it together with SeriMongo:

```bash
docker compose -f docker-compose.yml -f Docs/examples/docker-compose.collector.yml up --build
```

Then send to the Collector on port 4317 (gRPC) or 4318 (HTTP).

The Collector configuration is in `Docs/examples/otel-collector.yaml`. If you
already run a Collector, add an `otlphttp` exporter that points at SeriMongo.

## Configuration

Settings use standard ASP.NET Core configuration. Put them in
`SeriMongo/appsettings.json`, or set them as environment variables (use `__`
between levels).

| Setting | Default | Meaning |
| :-- | :-- | :-- |
| `ApplicationOptions__Database__ConnectionString` | `Data Source=Data/serimongo.db` (source), `Data Source=/data/serimongo.db` (Docker) | SQLite file that stores the logs. The folder is created if needed. |
| `ApplicationOptions__Seed__Enabled` | `false` (source), `true` (Docker Compose) | Load sample logs at startup when the database is empty. |
| `ApplicationOptions__Seed__ScriptPath` | `seed.sql` | SQL script used for seeding. In Docker it is `/app/seed.sql`. |
| `SimulatorOptions__TargetBaseUrl` | `http://localhost:51983` | Where the simulator sends logs. |

For example, to run from source with sample data:

```bash
cd SeriMongo
dotnet run --urls http://localhost:51983 \
  --ApplicationOptions:Seed:Enabled=true \
  --ApplicationOptions:Seed:ScriptPath=../seed.sql
```

## Troubleshooting

| Symptom | Cause and fix |
| :-- | :-- |
| `http://localhost:4200` refuses the connection | The Angular dev server is not running. Run `npm start` in `SeriMongo`, or use <http://localhost:51983> after `npm run build`. |
| `http://localhost:51983` returns 404 but `/swagger` works | The UI has not been built. Run `npm ci` and `npm run build` in `SeriMongo`. |
| `npm ci` or `ng` fails with an engine or version error | Your Node.js version is too old for Angular 22. Install 22.22.3+ or 24.15.0+, or use Docker. |
| Your exporter gets HTTP 415 | It is not sending OTLP/HTTP Protobuf or JSON. Set `OTEL_EXPORTER_OTLP_PROTOCOL` to `http/protobuf` or `http/json`. |
| Logs from your application never arrive, with no error | The exporter is probably using OTLP/gRPC, which SeriMongo does not support. Many exporters, Serilog included, drop failed batches silently. Switch it to `http/protobuf`, or send through the Collector. |
| UI shows no live updates | The SignalR connection to `/logs` failed. With `npm start`, check that the API is running on port 51983. |
| `$changeStream stage is only supported on replica sets` | You are running an old, MongoDB-based version. Update to the latest `master`. |
