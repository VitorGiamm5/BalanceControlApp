using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace BalanceControl.Observability;

internal sealed class ActivityLogEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null)
            return;

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("trace_id", activity.TraceId.ToHexString()));
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("span_id", activity.SpanId.ToHexString()));
    }
}
