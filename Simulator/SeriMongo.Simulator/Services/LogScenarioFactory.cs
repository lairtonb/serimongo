using System.Globalization;

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

    public bool TryCreate(string level, out SimulatedLog log)
    {
        var canonicalLevel = GetCanonicalLevel(level);
        if (canonicalLevel is null)
        {
            log = default!;
            return false;
        }

        log = Create(canonicalLevel);
        return true;
    }

    public IReadOnlyList<SimulatedLog> CreateBurst(int count)
    {
        var levels = SeverityNumbers.Keys.ToArray();
        var initialServices = ServiceNames.OrderBy(_ => Random.Shared.Next()).ToArray();
        var logs = new List<SimulatedLog>(count);

        for (var index = 0; index < count; index++)
        {
            var serviceName = index < initialServices.Length
                ? initialServices[index]
                : ServiceNames[Random.Shared.Next(ServiceNames.Length)];

            logs.Add(Create(levels[Random.Shared.Next(levels.Length)], serviceName));
        }

        return logs;
    }

    private static SimulatedLog Create(string level, string? serviceName = null)
    {
        var route = Routes[Random.Shared.Next(Routes.Length)];
        serviceName ??= ServiceNames[Random.Shared.Next(ServiceNames.Length)];
        var tenant = Tenants[Random.Shared.Next(Tenants.Length)];
        var region = Regions[Random.Shared.Next(Regions.Length)];
        var duration = Random.Shared.Next(8, 4800);
        var orderId = Random.Shared.Next(10_000, 99_999);
        var customerId = Random.Shared.Next(1_000, 9_999);
        var scenarioId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        var message = CreateMessage(level, serviceName, route, tenant, duration, orderId);
        var properties = new Dictionary<string, object?>
        {
            ["ScenarioId"] = scenarioId,
            ["ServiceName"] = serviceName,
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

        return new SimulatedLog(serviceName, level, SeverityNumbers[level], message, properties, exception);
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
