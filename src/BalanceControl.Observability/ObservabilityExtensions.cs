using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Configuration;
using Serilog.Sinks.OpenTelemetry;

namespace BalanceControl.Observability;

public static class ObservabilityExtensions
{
    public static ObservabilitySettings AddBalanceControlObservability(
        this IHostApplicationBuilder builder,
        string serviceName)
    {
        var settings = ObservabilitySettingsParser.Parse(
            builder.Configuration,
            builder.Environment,
            serviceName);

        builder.Services.AddSingleton(settings);

        if (!settings.Enabled)
            return settings;

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(settings.ResourceAttributes))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(CreateSampler(settings))
                    .AddSource(ObservabilitySettings.ActivitySourceName)
                    .AddSource("Npgsql")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments("/health")
                            && !context.Request.Path.StartsWithSegments("/health/ready")
                            && !context.Request.Path.StartsWithSegments("/health/live")
                            && !context.Request.Path.StartsWithSegments("/health/startup")
                            && !context.Request.Path.StartsWithSegments("/alive")
                            && !context.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation(options => options.RecordException = true);

                if (settings.TracesEnabled)
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(settings.OtlpEndpoint);
                        options.Protocol = settings.OtlpProtocol;
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(ObservabilitySettings.MeterName)
                    .AddMeter("Npgsql")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (settings.MetricsEnabled)
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(settings.OtlpEndpoint);
                        options.Protocol = settings.OtlpProtocol;
                    });
                }
            });

        return settings;
    }

    public static LoggerConfiguration ConfigureBalanceControlLogging(
        this LoggerConfiguration configuration,
        ObservabilitySettings settings)
    {
        configuration
            .Enrich.FromLogContext()
            .Enrich.With(new ActivityLogEnricher())
            .Enrich.WithProperty("service.name", settings.ServiceName)
            .Enrich.WithProperty("service.version", settings.ServiceVersion)
            .Enrich.WithProperty("service.instance.id", settings.ServiceInstanceId)
            .Enrich.WithProperty("deployment.environment.name", settings.DeploymentEnvironment)
            .Enrich.WithProperty("release.channel", settings.ReleaseChannel)
            .Enrich.WithProperty("vcs.revision", settings.VcsRevision)
            .WriteTo.Console(new BalanceControlJsonFormatter());

        if (!string.IsNullOrWhiteSpace(settings.SeqUrl))
        {
            configuration.WriteTo.Seq(settings.SeqUrl);
        }

        if (settings.LogsEnabled)
        {
            configuration.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = settings.OtlpEndpoint;
                options.Protocol = settings.OtlpProtocol == OpenTelemetry.Exporter.OtlpExportProtocol.Grpc
                    ? OtlpProtocol.Grpc
                    : OtlpProtocol.HttpProtobuf;
                options.ResourceAttributes = settings.ResourceAttributes
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                options.OnBeginSuppressInstrumentation = SuppressInstrumentationScope.Begin;
            });
        }

        return configuration;
    }

    private static Sampler CreateSampler(ObservabilitySettings settings)
        => settings.TraceSampler switch
        {
            "always_on" => new AlwaysOnSampler(),
            "always_off" => new AlwaysOffSampler(),
            "parentbased_always_on" => new ParentBasedSampler(new AlwaysOnSampler()),
            "parentbased_always_off" => new ParentBasedSampler(new AlwaysOffSampler()),
            "traceidratio" => new TraceIdRatioBasedSampler(settings.TraceSamplingRatio),
            "parentbased_traceidratio" => new ParentBasedSampler(
                new TraceIdRatioBasedSampler(settings.TraceSamplingRatio)),
            _ => throw new InvalidOperationException(
                $"Sampler nao suportado: {settings.TraceSampler}.")
        };
}
