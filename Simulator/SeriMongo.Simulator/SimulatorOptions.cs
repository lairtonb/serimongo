namespace SeriMongo.Simulator;

public sealed class SimulatorOptions
{
    public const string SectionName = "SimulatorOptions";

    public string TargetBaseUrl { get; set; } = "http://localhost:51983";

    public Uri GetTargetBaseUri()
    {
        var target = string.IsNullOrWhiteSpace(TargetBaseUrl)
            ? "http://localhost:51983"
            : TargetBaseUrl.Trim();

        return new Uri(target.TrimEnd('/') + "/", UriKind.Absolute);
    }
}
