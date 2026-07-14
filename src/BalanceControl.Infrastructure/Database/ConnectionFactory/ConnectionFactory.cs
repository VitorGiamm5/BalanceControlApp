using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Retry;
using BalanceControl.Infrastructure.Database.Configuration;

namespace BalanceControl.Infrastructure.Database.ConnectionFactory;

public class ConnectionFactory : IConnectionFactory
{
    private readonly string _writeConnectionString = string.Empty;
    private readonly string _readConnectionString = string.Empty;
    private readonly ILogger<ConnectionFactory> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public ConnectionFactory(IConfiguration configuration, ILogger<ConnectionFactory> logger)
    {
        _logger = logger;

        // Valida obrigatoriedade antes de subir a aplicação
        _writeConnectionString = configuration.GetConnectionString("PostgresWrite")
            ?? throw new InvalidOperationException("ConnectionStrings:PostgresWrite não configurada.");

        _readConnectionString = configuration.GetConnectionString("PostgresRead")
            ?? throw new InvalidOperationException("ConnectionStrings:PostgresRead não configurada.");

        DatabaseResilienceOptions databaseResilience = DatabaseResilienceOptions.FromConfiguration(configuration);

        _retryPolicy = Policy
            .Handle<NpgsqlException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: databaseResilience.MaxRetryAttempts,
                sleepDurationProvider: databaseResilience.RetryDelayForAttempt,
                onRetry: (exception, timeSpan, attempt, _) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Falha na conexão com o banco. Tentativa {Attempt} aguardando {DelaySeconds}s",
                        attempt,
                        timeSpan.TotalSeconds);
                });
    }

    public NpgsqlConnection CreateWriteConnection() =>
        new(_writeConnectionString);

    public NpgsqlConnection CreateReadConnection() =>
        new(_readConnectionString);

    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action) =>
        await _retryPolicy.ExecuteAsync(action);

    public async Task ExecuteWithRetryAsync(Func<Task> action) =>
        await _retryPolicy.ExecuteAsync(action);
}
