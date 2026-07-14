namespace SeriMongo.Simulator.Services;

public sealed record SimulatedSpan(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string ServiceName,
    string Name,
    int Kind,
    DateTimeOffset StartTimestamp,
    DateTimeOffset EndTimestamp,
    int StatusCode,
    string? StatusMessage,
    IReadOnlyDictionary<string, object?> Attributes);
