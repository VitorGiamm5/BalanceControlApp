using Microsoft.Extensions.Configuration;

namespace BalanceControl.Infrastructure.Database.Configuration;

public sealed class DatabaseResilienceOptions
{
    private const int DefaultConnectionTimeoutSeconds = 3;
    private const int DefaultCommandTimeoutSeconds = 3;
    private const int DefaultMaxRetryAttempts = 3;
    private const int DefaultRetryDelaySeconds = 1;
    private const int DefaultMaxRetryDelaySeconds = 2;

    public int ConnectionTimeoutSeconds { get; private init; } = DefaultConnectionTimeoutSeconds;
    public int CommandTimeoutSeconds { get; private init; } = DefaultCommandTimeoutSeconds;
    public int MaxRetryAttempts { get; private init; } = DefaultMaxRetryAttempts;
    public int RetryDelaySeconds { get; private init; } = DefaultRetryDelaySeconds;
    public int MaxRetryDelaySeconds { get; private init; } = DefaultMaxRetryDelaySeconds;

    public static DatabaseResilienceOptions FromConfiguration(IConfiguration configuration)
        => new()
        {
            ConnectionTimeoutSeconds = GetPositiveInt(
                configuration,
                "DatabaseResilience:ConnectionTimeoutSeconds",
                null,
                DefaultConnectionTimeoutSeconds),
            CommandTimeoutSeconds = GetPositiveInt(
                configuration,
                "DatabaseResilience:CommandTimeoutSeconds",
                null,
                DefaultCommandTimeoutSeconds),
            MaxRetryAttempts = GetPositiveInt(
                configuration,
                "DatabaseResilience:MaxRetryAttempts",
                "RetryPolicy:MaxRetryAttempts",
                DefaultMaxRetryAttempts),
            RetryDelaySeconds = GetPositiveInt(
                configuration,
                "DatabaseResilience:RetryDelaySeconds",
                "RetryPolicy:DelayBetweenRetriesInSeconds",
                DefaultRetryDelaySeconds),
            MaxRetryDelaySeconds = GetPositiveInt(
                configuration,
                "DatabaseResilience:MaxRetryDelaySeconds",
                null,
                DefaultMaxRetryDelaySeconds)
        };

    public TimeSpan RetryDelayForAttempt(int attemptNumber)
    {
        int boundedAttempt = Math.Max(1, attemptNumber);
        double delaySeconds = Math.Pow(RetryDelaySeconds, boundedAttempt);
        double boundedDelaySeconds = Math.Min(MaxRetryDelaySeconds, delaySeconds);

        return TimeSpan.FromSeconds(Math.Max(1, boundedDelaySeconds));
    }

    private static int GetPositiveInt(
        IConfiguration configuration,
        string key,
        string? legacyKey,
        int defaultValue)
    {
        int? value = configuration.GetValue<int?>(key)
            ?? (legacyKey is null ? null : configuration.GetValue<int?>(legacyKey));

        return value is > 0
            ? value.Value
            : defaultValue;
    }

}
