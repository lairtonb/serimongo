using System.Globalization;
using System.Security.Cryptography;

namespace SeriMongo.Simulator.Services;

public sealed class LogScenarioFactory
{
    public static readonly string[] ServiceNames =
    [
        "customer-api",
        "customer-worker",
        "order-api",
        "order-consumer",
        "order-producer",
        "checkout-api",
        "payment-api"
    ];

    private static readonly IReadOnlyDictionary<string, int> SeverityNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Trace"] = 1,
        ["Debug"] = 5,
        ["Information"] = 9,
        ["Warning"] = 13,
        ["Error"] = 17,
        ["Fatal"] = 21
    };

    private static readonly string[] Routes =
    [
        "/api/orders",
        "/api/payments",
        "/api/customers",
        "/api/inventory",
        "/jobs/reconciliation",
        "/workers/notifications"
    ];

    private static readonly string[] Regions = ["us-east", "us-west", "eu-north", "br-south"];
    private static readonly string[] Tenants = ["contoso", "fabrikam", "northwind", "adatum"];

    public bool TryCreateOperation(string level, out SimulatedOperation operation)
    {
        var canonicalLevel = GetCanonicalLevel(level);
        if (canonicalLevel is null)
        {
            operation = default!;
            return false;
        }

        operation = CreateOperation(canonicalLevel);
        return true;
    }

    public IReadOnlyList<SimulatedOperation> CreateBurst(int count)
    {
        var levels = SeverityNumbers.Keys.ToArray();
        var initialServices = ServiceNames.OrderBy(_ => Random.Shared.Next()).ToArray();
        var operations = new List<SimulatedOperation>(count);

        for (var index = 0; index < count; index++)
        {
            var serviceName = index < initialServices.Length
                ? initialServices[index]
                : ServiceNames[Random.Shared.Next(ServiceNames.Length)];

            operations.Add(CreateOperation(levels[Random.Shared.Next(levels.Length)], serviceName));
        }

        return operations;
    }

    private static SimulatedOperation CreateOperation(string level, string? serviceName = null)
    {
        var route = Routes[Random.Shared.Next(Routes.Length)];
        serviceName ??= ServiceNames[Random.Shared.Next(ServiceNames.Length)];
        var tenant = Tenants[Random.Shared.Next(Tenants.Length)];
        var region = Regions[Random.Shared.Next(Regions.Length)];
        var duration = Random.Shared.Next(8, 4800);
        var orderId = Random.Shared.Next(10_000, 99_999);
        var customerId = Random.Shared.Next(1_000, 9_999);
        var scenarioId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        var traceId = CreateIdentifier(byteCount: 16);
        var rootSpanId = CreateIdentifier(byteCount: 8);
        var validationSpanId = CreateIdentifier(byteCount: 8);
        var dependencySpanId = CreateIdentifier(byteCount: 8);
        var processingSpanId = CreateIdentifier(byteCount: 8);
        var startTimestamp = DateTimeOffset.UtcNow.AddMilliseconds(-duration);
        var endTimestamp = DateTimeOffset.UtcNow;
        var validationEnd = startTimestamp.AddMilliseconds(Math.Max(1, duration * 0.15));
        var dependencyStart = validationEnd;
        var dependencyEnd = startTimestamp.AddMilliseconds(Math.Max(2, duration * 0.65));
        var processingStart = dependencyStart.AddMilliseconds(1);
        var processingEnd = dependencyEnd.AddMilliseconds(-1);
        var dependencyServiceName = serviceName == "payment-api" ? "order-api" : "payment-api";
        var hasError = level is "Error" or "Fatal";
        var message = CreateMessage(level, dependencyServiceName, route, tenant, duration, orderId);
        var properties = new Dictionary<string, object?>
        {
            ["ScenarioId"] = scenarioId,
            ["ServiceName"] = dependencyServiceName,
            ["CustomerId"] = customerId,
            ["OrderId"] = orderId,
            ["Tenant"] = tenant,
            ["Region"] = region,
            ["Route"] = route,
            ["DurationMs"] = duration,
            ["Host"] = Environment.MachineName,
            ["Simulator"] = "SeriMongo.Simulator"
        };

        var exception = CreateException(level, route, orderId);
        if (exception is not null)
        {
            properties["exception.type"] = level == "Fatal" ? "System.OutOfMemoryException" : "System.TimeoutException";
            properties["exception.message"] = message;
            properties["exception.stacktrace"] = exception;
        }

        var rootAttributes = new Dictionary<string, object?>
        {
            ["http.request.method"] = "POST",
            ["url.path"] = route,
            ["tenant.id"] = tenant,
            ["order.id"] = orderId
        };
        var spans = new[]
        {
            new SimulatedSpan(traceId, rootSpanId, null, serviceName, $"POST {route}", 2, startTimestamp, endTimestamp, hasError ? 2 : 1, hasError ? message : null, rootAttributes),
            new SimulatedSpan(traceId, validationSpanId, rootSpanId, serviceName, "Validate request", 1, startTimestamp, validationEnd, 1, null, new Dictionary<string, object?>
            {
                ["serimongo.operation.phase"] = "validation",
                ["tenant.id"] = tenant
            }),
            new SimulatedSpan(traceId, dependencySpanId, rootSpanId, serviceName, $"Call {dependencyServiceName}", 3, dependencyStart, dependencyEnd, hasError ? 2 : 1, hasError ? message : null, new Dictionary<string, object?>
            {
                ["peer.service"] = dependencyServiceName,
                ["server.address"] = dependencyServiceName
            }),
            new SimulatedSpan(traceId, processingSpanId, dependencySpanId, dependencyServiceName, $"Process {route}", 2, processingStart, processingEnd, hasError ? 2 : 1, hasError ? message : null, new Dictionary<string, object?>
            {
                ["serimongo.operation.phase"] = "processing",
                ["order.id"] = orderId,
                ["customer.id"] = customerId
            })
        };
        var log = new SimulatedLog(
            dependencyServiceName,
            level,
            SeverityNumbers[level],
            message,
            properties,
            traceId,
            processingSpanId,
            exception);

        return new SimulatedOperation([log], spans);
    }

    private static string CreateIdentifier(int byteCount)
    {
        return Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(byteCount));
    }

    private static string? GetCanonicalLevel(string level)
    {
        return SeverityNumbers.Keys.FirstOrDefault(candidate =>
            string.Equals(candidate, level, StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateMessage(string level, string serviceName, string route, string tenant, int duration, int orderId)
    {
        return level switch
        {
            "Trace" => $"{serviceName} trace probe crossed {route} for tenant {tenant} in {duration}ms.",
            "Debug" => $"{serviceName} debug snapshot captured order {orderId} for {tenant}.",
            "Information" => $"{serviceName} processed request {route} for tenant {tenant} in {duration}ms.",
            "Warning" => $"{serviceName} slow request on {route}: {duration}ms for tenant {tenant}.",
            "Error" => $"{serviceName} operation failed for order {orderId} on {route}.",
            "Fatal" => $"{serviceName} fatal worker failure while processing order {orderId}.",
            _ => $"{serviceName} {level} simulator event on {route}."
        };
    }

    private static string? CreateException(string level, string route, int orderId)
    {
        return level switch
        {
            "Error" => $"System.TimeoutException: Request timed out on {route} for order {orderId}\n   at CheckoutWorker.ProcessAsync()\n   at SeriMongo.Simulator.EmitAsync()",
            "Fatal" => $"System.OutOfMemoryException: Worker exhausted memory for order {orderId}\n   at BatchWorker.ProcessAsync()\n   at SeriMongo.Simulator.EmitAsync()",
            _ => null
        };
    }
}
