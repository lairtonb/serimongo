namespace SeriMongo.Simulator.Services;

public sealed record SimulatedOperation(
    IReadOnlyList<SimulatedLog> Logs,
    IReadOnlyList<SimulatedSpan> Spans);
