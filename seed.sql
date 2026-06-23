BEGIN TRANSACTION;

WITH RECURSIVE seq(n) AS (
    SELECT 1
    UNION ALL
    SELECT n + 1 FROM seq WHERE n < 3000
), generated AS (
    SELECT
        n,
        CASE ((random() % 7) + 7) % 7
            WHEN 0 THEN 'customer-api'
            WHEN 1 THEN 'customer-worker'
            WHEN 2 THEN 'order-api'
            WHEN 3 THEN 'order-consumer'
            WHEN 4 THEN 'order-producer'
            WHEN 5 THEN 'checkout-api'
            ELSE 'payment-api'
        END AS ServiceName,
        CASE n % 8
            WHEN 0 THEN 'Verbose'
            WHEN 1 THEN 'Trace'
            WHEN 2 THEN 'Debug'
            WHEN 3 THEN 'Information'
            WHEN 4 THEN 'Warning'
            WHEN 5 THEN 'Error'
            WHEN 6 THEN 'Fatal'
            ELSE 'Critical'
        END AS Level,
        CASE n % 12
            WHEN 0 THEN 'HTTP request completed for checkout cart in ' || (20 + (n % 980)) || ' ms'
            WHEN 1 THEN 'Payment authorization declined for order ORD-' || printf('%06d', n)
            WHEN 2 THEN 'Inventory reservation succeeded for SKU-' || (1000 + (n % 650))
            WHEN 3 THEN 'Background sync processed tenant tenant-' || (n % 24)
            WHEN 4 THEN 'Cache refresh skipped because etag matched for catalog segment ' || (n % 32)
            WHEN 5 THEN 'Email notification queued for customer ' || (100000 + n)
            WHEN 6 THEN 'OpenTelemetry batch accepted with ' || (1 + (n % 64)) || ' records'
            WHEN 7 THEN 'SQL query plan exceeded soft threshold for request ' || lower(hex(randomblob(4)))
            WHEN 8 THEN 'Feature flag evaluated for region ' || (CASE n % 6 WHEN 0 THEN 'br-south' WHEN 1 THEN 'east-us' WHEN 2 THEN 'west-europe' WHEN 3 THEN 'uksouth' WHEN 4 THEN 'japan-east' ELSE 'australia-east' END)
            WHEN 9 THEN 'User session renewed for principal user-' || (n % 480)
            WHEN 10 THEN 'SignalR client reconnected after ' || (1 + (n % 12)) || ' attempts'
            ELSE 'Scheduled cleanup removed ' || (n % 70) || ' stale records'
        END AS RenderedMessage
    FROM seq
)
INSERT INTO LogEntries (Id, TimestampUtc, Level, RenderedMessage, Exception, PropertiesJson)
SELECT
    lower(hex(randomblob(16))) AS Id,
    strftime('%Y-%m-%dT%H:%M:%fZ', 'now', printf('-%d minutes', (n * 7) % 129600)) AS TimestampUtc,
    Level,
    RenderedMessage,
    CASE
        WHEN Level IN ('Error', 'Fatal', 'Critical') THEN
            'System.InvalidOperationException: Synthetic failure for seed record ' || n || char(10) ||
            '   at SeriMongo.Seed.Worker.ProcessAsync() in /seed/Worker.cs:line ' || (20 + (n % 140)) || char(10) ||
            '   at SeriMongo.Seed.Pipeline.InvokeAsync() in /seed/Pipeline.cs:line ' || (5 + (n % 50))
        ELSE NULL
    END AS Exception,
    json_object(
        'CustomerId', 100000 + n,
        'OrderId', 'ORD-' || printf('%06d', n),
        'Tenant', 'tenant-' || (n % 24),
        'Country', CASE n % 10
            WHEN 0 THEN 'Brazil'
            WHEN 1 THEN 'United States'
            WHEN 2 THEN 'Germany'
            WHEN 3 THEN 'Portugal'
            WHEN 4 THEN 'Japan'
            WHEN 5 THEN 'Australia'
            WHEN 6 THEN 'Canada'
            WHEN 7 THEN 'Mexico'
            WHEN 8 THEN 'United Kingdom'
            ELSE 'Spain'
        END,
        'Region', CASE n % 6
            WHEN 0 THEN 'br-south'
            WHEN 1 THEN 'east-us'
            WHEN 2 THEN 'west-europe'
            WHEN 3 THEN 'uksouth'
            WHEN 4 THEN 'japan-east'
            ELSE 'australia-east'
        END,
        'MachineName', 'seed-node-' || printf('%02d', n % 18),
        'resource.service.name', ServiceName,
        'RequestPath', CASE n % 7
            WHEN 0 THEN '/api/checkout'
            WHEN 1 THEN '/api/payments/authorize'
            WHEN 2 THEN '/api/catalog/search'
            WHEN 3 THEN '/api/orders'
            WHEN 4 THEN '/api/otlp/v1/logs'
            WHEN 5 THEN '/jobs/sync'
            ELSE '/hubs/logs'
        END,
        'SourceContext', CASE n % 8
            WHEN 0 THEN 'SeriMongo.Seed.HttpPipeline'
            WHEN 1 THEN 'SeriMongo.Seed.PaymentGateway'
            WHEN 2 THEN 'SeriMongo.Seed.Inventory'
            WHEN 3 THEN 'SeriMongo.Seed.Worker'
            WHEN 4 THEN 'SeriMongo.Seed.Cache'
            WHEN 5 THEN 'SeriMongo.Seed.Notifications'
            WHEN 6 THEN 'SeriMongo.Seed.OtlpReceiver'
            ELSE 'SeriMongo.Seed.SignalR'
        END,
        'ElapsedMilliseconds', 5 + ((n * 37) % 2500),
        'CorrelationId', lower(hex(randomblob(8))) || '-' || printf('%04d', n),
        'TraceId', lower(hex(randomblob(16))),
        'SpanId', lower(hex(randomblob(8))),
        'Feature', CASE n % 5
            WHEN 0 THEN 'checkout'
            WHEN 1 THEN 'payments'
            WHEN 2 THEN 'catalog'
            WHEN 3 THEN 'notifications'
            ELSE 'observability'
        END,
        'Environment', CASE n % 4 WHEN 0 THEN 'Development' WHEN 1 THEN 'Staging' WHEN 2 THEN 'Production' ELSE 'LoadTest' END,
        'Batch', n / 100,
        'SyntheticSeed', 1
    ) AS PropertiesJson
FROM generated
WHERE NOT EXISTS (SELECT 1 FROM LogEntries LIMIT 1);

COMMIT;
