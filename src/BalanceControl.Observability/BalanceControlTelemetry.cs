using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BalanceControl.Observability;

public static class BalanceControlTelemetry
{
    public static readonly ActivitySource ActivitySource =
        new(ObservabilitySettings.ActivitySourceName);

    public static readonly Meter Meter =
        new(ObservabilitySettings.MeterName);
}
