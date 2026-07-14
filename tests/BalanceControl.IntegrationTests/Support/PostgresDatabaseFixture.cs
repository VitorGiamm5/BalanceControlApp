using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace BalanceControl.IntegrationTests.Support;

public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private Respawner? _respawner;

    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("balance_control_tests")
        .WithUsername("balance_test")
        .WithPassword("balance_test_password")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["balance_control"]
        });

        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
