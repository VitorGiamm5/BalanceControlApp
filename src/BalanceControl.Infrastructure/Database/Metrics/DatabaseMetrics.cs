using Prometheus;

namespace BalanceControl.Infrastructure.Database.Metrics;

public static class DatabaseMetrics
{
    public static readonly Counter ReadOperationsTotal = Prometheus.Metrics
        .CreateCounter(
            "app_database_read_operations_total",
            "Total number of database read operations",
            labelNames: ["operation", "context"]);

    public static readonly Counter WriteOperationsTotal = Prometheus.Metrics
        .CreateCounter(
            "app_database_write_operations_total",
            "Total number of database write operations",
            labelNames: ["operation", "context"]);

    public static readonly Histogram CommandDurationSeconds = Prometheus.Metrics
        .CreateHistogram(
            "app_database_command_duration_seconds",
            "EF Core command execution time in seconds",
            labelNames: ["operation", "context"],
            new HistogramConfiguration
            {
                Buckets = [.001, .005, .01, .025, .05, .1, .25, .5, 1, 2]
            });

    public static readonly Counter RetriesTotal = Prometheus.Metrics
        .CreateCounter(
            "app_database_retries_total",
            "Total number of database command retries via Polly",
            labelNames: ["operation"]);

    static DatabaseMetrics()
    {
        ReadOperationsTotal.WithLabels("select", "ApplicationDbContext").Inc(0);
        ReadOperationsTotal.WithLabels("select", "ReadOnlyDbContext").Inc(0);

        foreach (string operation in new[] { "insert", "update", "delete", "merge" })
        {
            WriteOperationsTotal.WithLabels(operation, "ApplicationDbContext").Inc(0);
        }

        foreach (string operation in new[] { "select", "insert", "update", "delete", "merge", "unknown" })
        {
            foreach (string context in new[] { "ApplicationDbContext", "ReadOnlyDbContext" })
            {
                CommandDurationSeconds.WithLabels(operation, context).Observe(0);
            }
            RetriesTotal.WithLabels(operation).Inc(0);
        }
    }
}
