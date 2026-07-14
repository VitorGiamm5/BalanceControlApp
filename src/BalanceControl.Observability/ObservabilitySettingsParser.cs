using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;

namespace BalanceControl.Observability;

public static class ObservabilitySettingsParser
{
    public static ObservabilitySettings Parse(
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceName)
    {
        var profile = ParseProfile(configuration["OBSERVABILITY_PROFILE"]);
        var enabled = ParseBoolean(configuration, "OBSERVABILITY_ENABLED", profile != ObservabilityProfile.Off);

        if (!enabled)
            profile = ObservabilityProfile.Off;

        var payloadCaptureEnabled = ParseBoolean(
            configuration,
            "OBSERVABILITY_PAYLOAD_CAPTURE_ENABLED",
            defaultValue: false);

        if (payloadCaptureEnabled && profile is not (ObservabilityProfile.Diagnostic or ObservabilityProfile.Full))
        {
            throw new InvalidOperationException(
                "OBSERVABILITY_PAYLOAD_CAPTURE_ENABLED so pode ser habilitado nos perfis diagnostic ou full.");
        }

        var serviceVersion = FirstNotEmpty(
            configuration["OTEL_SERVICE_VERSION"],
            configuration["SERVICE_VERSION"],
            configuration["Version"],
            "unknown");
        var (traceSampler, traceSamplingRatio) = ParseSampler(configuration, profile);

        return new ObservabilitySettings
        {
            Enabled = enabled,
            Profile = profile,
            ServiceName = FirstNotEmpty(configuration["OTEL_SERVICE_NAME"], serviceName),
            ServiceVersion = serviceVersion,
            ServiceInstanceId = FirstNotEmpty(
                configuration["OTEL_SERVICE_INSTANCE_ID"],
                Environment.GetEnvironmentVariable("HOSTNAME"),
                $"{Environment.MachineName}-{Environment.ProcessId}"),
            DeploymentEnvironment = NormalizeEnvironment(environment.EnvironmentName),
            OtlpEndpoint = FirstNotEmpty(
                configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
                "http://localhost:4317"),
            OtlpProtocol = ParseProtocol(configuration["OTEL_EXPORTER_OTLP_PROTOCOL"]),
            TraceSampler = traceSampler,
            TraceSamplingRatio = traceSamplingRatio,
            LogsEnabled = enabled && ExporterEnabled(configuration["OTEL_LOGS_EXPORTER"]),
            MetricsEnabled = enabled && ExporterEnabled(configuration["OTEL_METRICS_EXPORTER"]),
            TracesEnabled = enabled && ExporterEnabled(configuration["OTEL_TRACES_EXPORTER"]),
            SeqUrl = NullIfEmpty(configuration["SEQ_URL"]),
            SqlTextEnabled = ParseBoolean(configuration, "OBSERVABILITY_SQL_TEXT_ENABLED", false),
            RedisDetailEnabled = ParseBoolean(configuration, "OBSERVABILITY_REDIS_DETAIL_ENABLED", false),
            BusinessIdsEnabled = ParseBoolean(configuration, "OBSERVABILITY_BUSINESS_IDS_ENABLED", true),
            PayloadCaptureEnabled = payloadCaptureEnabled,
            ReleaseChannel = FirstNotEmpty(
                configuration["OBSERVABILITY_RELEASE_CHANNEL"],
                configuration["RELEASE_CHANNEL"],
                "development"),
            BaseVersion = FirstNotEmpty(configuration["SERVICE_BASE_VERSION"], serviceVersion),
            VcsRevision = FirstNotEmpty(configuration["VCS_REF"], "unknown"),
            ContainerImageName = FirstNotEmpty(configuration["CONTAINER_IMAGE_NAME"], serviceName),
            ContainerImageTags = FirstNotEmpty(configuration["CONTAINER_IMAGE_TAGS"], serviceVersion),
            BenchmarkRunId = NullIfEmpty(configuration["OBSERVABILITY_BENCHMARK_RUN_ID"]),
            BenchmarkScenario = NullIfEmpty(configuration["OBSERVABILITY_BENCHMARK_SCENARIO"]),
            BenchmarkCandidate = NullIfEmpty(configuration["OBSERVABILITY_BENCHMARK_CANDIDATE"]),
            AdditionalResourceAttributes = ParseResourceAttributes(
                configuration["OTEL_RESOURCE_ATTRIBUTES"])
        };
    }

    private static ObservabilityProfile ParseProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ObservabilityProfile.Standard;

        if (Enum.TryParse<ObservabilityProfile>(value, ignoreCase: true, out var profile))
            return profile;

        throw new InvalidOperationException(
            $"OBSERVABILITY_PROFILE invalido: '{value}'. Valores aceitos: off, minimal, standard, benchmark, diagnostic, full.");
    }

    private static OtlpExportProtocol ParseProtocol(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "grpc" => OtlpExportProtocol.Grpc,
            "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => throw new InvalidOperationException(
                $"OTEL_EXPORTER_OTLP_PROTOCOL invalido: '{value}'. Use grpc ou http/protobuf.")
        };

    private static bool ParseBoolean(
        IConfiguration configuration,
        string key,
        bool defaultValue)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (bool.TryParse(value, out var parsed))
            return parsed;

        throw new InvalidOperationException($"{key} deve ser true ou false.");
    }

    private static bool ExporterEnabled(string? value)
        => !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);

    private static double GetSamplingRatio(ObservabilityProfile profile)
        => profile switch
        {
            ObservabilityProfile.Off => 0,
            ObservabilityProfile.Minimal => 0.05,
            ObservabilityProfile.Standard => 0.10,
            ObservabilityProfile.Benchmark => 0.25,
            ObservabilityProfile.Diagnostic or ObservabilityProfile.Full => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };

    private static (string Sampler, double Ratio) ParseSampler(
        IConfiguration configuration,
        ObservabilityProfile profile)
    {
        var sampler = configuration["OTEL_TRACES_SAMPLER"]?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(sampler))
            return ("parentbased_traceidratio", GetSamplingRatio(profile));

        return sampler switch
        {
            "always_on" or "parentbased_always_on" => (sampler, 1),
            "always_off" or "parentbased_always_off" => (sampler, 0),
            "traceidratio" or "parentbased_traceidratio" => (
                sampler,
                ParseSamplingRatio(configuration["OTEL_TRACES_SAMPLER_ARG"])),
            _ => throw new InvalidOperationException(
                $"OTEL_TRACES_SAMPLER invalido: '{sampler}'.")
        };
    }

    private static double ParseSamplingRatio(string? value)
    {
        if (!double.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var ratio)
            || ratio is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "OTEL_TRACES_SAMPLER_ARG deve ser um numero entre 0 e 1.");
        }

        return ratio;
    }

    private static string NormalizeEnvironment(string environmentName)
        => environmentName.Trim().ToLowerInvariant() switch
        {
            "development" => "development",
            "test" or "testing" => "test",
            "staging" => "staging",
            "production" => "production",
            var value => value
        };

    private static string FirstNotEmpty(params string?[] values)
        => values.First(value => !string.IsNullOrWhiteSpace(value))!;

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyDictionary<string, string> ParseResourceAttributes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new Dictionary<string, string>();

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = entry.IndexOf('=');
            if (separator <= 0 || separator == entry.Length - 1)
            {
                throw new InvalidOperationException(
                    $"OTEL_RESOURCE_ATTRIBUTES invalido: '{entry}'. Use chave=valor separado por virgulas.");
            }

            attributes[entry[..separator].Trim()] = entry[(separator + 1)..].Trim();
        }

        return attributes;
    }
}
