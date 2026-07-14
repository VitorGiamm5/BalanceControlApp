using System.Diagnostics;

namespace BalanceControl.Application.Telemetry;

internal static class AppActivitySource
{
    // Must match ObservabilitySettings.ActivitySourceName so OTel picks up these spans.
    internal static readonly ActivitySource Source = new("BalanceControl");
}
