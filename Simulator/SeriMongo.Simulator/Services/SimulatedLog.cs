namespace SeriMongo.Simulator.Services;

public sealed record SimulatedLog(
    string Level,
    int SeverityNumber,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    string? Exception = null);
