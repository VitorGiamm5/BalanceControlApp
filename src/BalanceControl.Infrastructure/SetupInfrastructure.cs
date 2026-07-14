using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Retry;
using BalanceControl.Domain.Repositories.Balances;
using BalanceControl.Domain.Security;
using BalanceControl.Infrastructure.Database.Configuration;
using BalanceControl.Infrastructure.Database.Contexts;
using BalanceControl.Infrastructure.Database.Interceptors;
using BalanceControl.Infrastructure.Database.Metrics;
using BalanceControl.Infrastructure.Database.Repositories.Balances;

namespace BalanceControl.Infrastructure;

public static class SetupInfrastructure
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddScoped<ICurrentUserContext, SystemCurrentUserContext>();

        DatabaseResilienceOptions databaseResilience = DatabaseResilienceOptions.FromConfiguration(configuration);

        services.AddSingleton<ResiliencePipeline>(sp =>
        {
            ILogger<ResilienceInterceptor> logger = sp.GetRequiredService<ILogger<ResilienceInterceptor>>();

            return new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = databaseResilience.MaxRetryAttempts,
                    Delay = TimeSpan.FromSeconds(databaseResilience.RetryDelaySeconds),
                    MaxDelay = TimeSpan.FromSeconds(databaseResilience.MaxRetryDelaySeconds),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<NpgsqlException>(ex => ResilienceInterceptor.IsTransient(ex))
                        .Handle<TimeoutException>(),
                    OnRetry = args =>
                    {
                        string operation = args.Context.Properties.TryGetValue(
                            ResilienceInterceptor.OperationKey, out string? op) ? op : "unknown";

                        logger.LogWarning(
                            args.Outcome.Exception,
                            "Database retry {Attempt}/{Max}. Next attempt in {Delay}.",
                            args.AttemptNumber + 1,
                            databaseResilience.MaxRetryAttempts,
                            args.RetryDelay);

                        DatabaseMetrics.RetriesTotal.WithLabels(operation).Inc();
                        return ValueTask.CompletedTask;
                    }
                })
                .AddTimeout(TimeSpan.FromSeconds(databaseResilience.CommandTimeoutSeconds))
                .Build();
        });

        services.AddSingleton<ResilienceInterceptor>();

        string writeConnectionString = configuration.GetConnectionString("PostgresWrite")
            ?? throw new InvalidOperationException("Connection string 'PostgresWrite' not found.");

        NpgsqlDataSourceBuilder writeDataSourceBuilder = new(writeConnectionString)
        {
            ConnectionStringBuilder =
            {
                IncludeErrorDetail = true,
                Timeout = databaseResilience.ConnectionTimeoutSeconds
            }
        };

        services.AddSingleton(writeDataSourceBuilder.Build());

        static RetryPolicy CreateRetryPolicy(DatabaseResilienceOptions databaseResilience, ILogger logger) =>
            Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount: databaseResilience.MaxRetryAttempts,
                    sleepDurationProvider: attempt => databaseResilience.RetryDelayForAttempt(attempt),
                    onRetry: (exception, timespan, attempt, _) =>
                    {
                        logger.LogWarning(
                            exception,
                            "Startup database retry {Attempt}. Next attempt in {RetryDelay}.",
                            attempt,
                            timespan);
                    });

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            ILogger logger = serviceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(SetupInfrastructure).FullName!);

            RetryPolicy retryPolicy = CreateRetryPolicy(databaseResilience, logger);

            retryPolicy.Execute(() =>
            {
                NpgsqlDataSource dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();

                options
                    .EnableDetailedErrors()
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                    .UseNpgsql(dataSource, npgsql =>
                    {
                        npgsql.CommandTimeout(databaseResilience.CommandTimeoutSeconds);
                        npgsql.MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            "balance_control");
                        npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    });

                options.AddInterceptors(serviceProvider.GetRequiredService<ResilienceInterceptor>());
            });
        });

        services.AddDbContext<ReadOnlyDbContext>((serviceProvider, options) =>
        {
            ILogger logger = serviceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(SetupInfrastructure).FullName!);

            RetryPolicy retryPolicy = CreateRetryPolicy(databaseResilience, logger);

            retryPolicy.Execute(() =>
            {
                string connectionString = configuration.GetConnectionString("PostgresRead")
                    ?? writeConnectionString;

                options
                    .UseNpgsql(connectionString, npgsql =>
                    {
                        npgsql.CommandTimeout(databaseResilience.CommandTimeoutSeconds);
                    })
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

                options.AddInterceptors(serviceProvider.GetRequiredService<ResilienceInterceptor>());
            });
        });

        services.TryAddScoped<IBalanceRepository, BalanceRepository>();

        return services;
    }

    public static IServiceCollection AddInfrastructureHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string writeConnectionString = configuration.GetConnectionString("PostgresWrite")
            ?? throw new InvalidOperationException("Connection string 'PostgresWrite' not found.");

        services.AddHealthChecks()
            .AddNpgSql(
                writeConnectionString,
                name: "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }
}
