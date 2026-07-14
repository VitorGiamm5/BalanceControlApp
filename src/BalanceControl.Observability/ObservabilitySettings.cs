using OpenTelemetry.Exporter;

namespace BalanceControl.Observability;

public sealed record ObservabilitySettings
{
    public const string ActivitySourceName = "BalanceControl";
    public const string MeterName = "BalanceControl";

    public required bool Enabled { get; init; }
    public required ObservabilityProfile Profile { get; init; }
    public required string ServiceName { get; init; }
    public required string ServiceVersion { get; init; }
    public required string ServiceInstanceId { get; init; }
    public required string DeploymentEnvironment { get; init; }
    public required string OtlpEndpoint { get; init; }
    public required OtlpExportProtocol OtlpProtocol { get; init; }
    public required string TraceSampler { get; init; }
    public required double TraceSamplingRatio { get; init; }
    public required bool LogsEnabled { get; init; }
    public required bool MetricsEnabled { get; init; }
    public required bool TracesEnabled { get; init; }
    public string? SeqUrl { get; init; }
    public required bool SqlTextEnabled { get; init; }
    public required bool RedisDetailEnabled { get; init; }
    public required bool BusinessIdsEnabled { get; init; }
    public required bool PayloadCaptureEnabled { get; init; }
    public required string ReleaseChannel { get; init; }
    public required string BaseVersion { get; init; }
    public required string VcsRevision { get; init; }
    public required string ContainerImageName { get; init; }
    public required string ContainerImageTags { get; init; }
    public string? BenchmarkRunId { get; init; }
    public string? BenchmarkScenario { get; init; }
    public string? BenchmarkCandidate { get; init; }
    public IReadOnlyDictionary<string, string> AdditionalResourceAttributes { get; init; } =
        new Dictionary<string, string>();

    public IReadOnlyDictionary<string, object> ResourceAttributes
    {
        get
        {
            var attributes = AdditionalResourceAttributes.ToDictionary(
                pair => pair.Key,
                pair => (object)pair.Value);

            attributes["service.namespace"] = "balancecontrolapp";
            attributes["service.name"] = ServiceName;
            attributes["service.version"] = ServiceVersion;
            attributes["service.instance.id"] = ServiceInstanceId;
            attributes["deployment.environment.name"] = DeploymentEnvironment;
            attributes["container.image.name"] = ContainerImageName;
            attributes["container.image.tags"] = ContainerImageTags;
            attributes["vcs.ref.head.revision"] = VcsRevision;
            attributes["balancecontrol.release.channel"] = ReleaseChannel;
            attributes["balancecontrol.base.version"] = BaseVersion;

            AddOptional(attributes, "balancecontrol.benchmark.run_id", BenchmarkRunId);
            AddOptional(attributes, "balancecontrol.benchmark.scenario", BenchmarkScenario);
            AddOptional(attributes, "balancecontrol.benchmark.candidate", BenchmarkCandidate);

            return attributes;
        }
    }

    private static void AddOptional(
        IDictionary<string, object> attributes,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            attributes[key] = value;
    }
}
