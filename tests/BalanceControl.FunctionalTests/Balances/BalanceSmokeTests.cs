using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace BalanceControl.FunctionalTests.Balances;

public class BalanceSmokeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("balance_control_tests")
        .WithUsername("balance_test")
        .WithPassword("balance_test_password")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        SetEnvironmentConfiguration(_postgres.GetConnectionString());
        _factory = new BalanceSmokeWebApplicationFactory(_postgres.GetConnectionString());
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateTokenAsync(_client));
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
        ClearEnvironmentConfiguration();
    }

    [Fact]
    public async Task Balance_Api_Should_Process_Replay_And_Return_Statement()
    {
        var userId = $"smoke-{Guid.NewGuid():N}";
        var firstOperationId = Guid.NewGuid();

        var first = await PostAdjustmentAsync(userId, firstOperationId, 100m);
        first.GetProperty("applied").GetBoolean().Should().BeTrue();
        first.GetProperty("balance").GetDecimal().Should().Be(100m);

        var replay = await PostAdjustmentAsync(userId, firstOperationId, 100m);
        replay.GetProperty("applied").GetBoolean().Should().BeFalse();
        replay.GetProperty("balance").GetDecimal().Should().Be(100m);

        var second = await PostAdjustmentAsync(userId, Guid.NewGuid(), -40m);
        second.GetProperty("balance").GetDecimal().Should().Be(60m);

        var balanceResponse = await _client.GetAsync($"/api/v1/balances/{userId}");
        balanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var balance = await ReadDataAsync(balanceResponse);
        balance.GetProperty("balance").GetDecimal().Should().Be(60m);

        var statementResponse = await _client.GetAsync($"/api/v1/balances/{userId}/statement?page=1&pageSize=10");
        statementResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var statement = await ReadDataAsync(statementResponse);
        statement.GetProperty("totalItems").GetInt64().Should().Be(2);
    }

    [Fact]
    public async Task Balance_Api_Should_Require_Bearer_Token()
    {
        using var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync($"/api/v1/balances/anonymous-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Balance_Api_Should_Return_NotFound_For_Unknown_User()
    {
        var userId = $"unknown-{Guid.NewGuid():N}";

        var balanceResponse = await _client.GetAsync($"/api/v1/balances/{userId}");
        var statementResponse = await _client.GetAsync($"/api/v1/balances/{userId}/statement?page=1&pageSize=10");

        balanceResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        statementResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Balance_Api_Should_Return_Conflict_When_OperationId_Is_Reused_With_Different_Payload()
    {
        var userId = $"conflict-{Guid.NewGuid():N}";
        var operationId = Guid.NewGuid();

        await PostAdjustmentAsync(userId, operationId, 100m);
        var response = await _client.PostAsJsonAsync(
            "/api/v1/balances/adjustments",
            new
            {
                userId,
                operationId,
                amount = 101m,
                description = "changed payload"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await ReadFirstErrorAsync(response);
        error.GetProperty("message").GetString().Should().Contain("payload diferente");
    }

    [Fact]
    public async Task Balance_Api_Should_Return_UnprocessableEntity_For_Invalid_Adjustment()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/balances/adjustments",
            new
            {
                userId = "user-0001",
                operationId = Guid.NewGuid(),
                amount = 0m,
                description = "zero amount"
            });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await ReadFirstErrorAsync(response);
        error.GetProperty("message").GetString().Should().Be("Erro de validação.");
    }

    [Fact]
    public async Task Balance_Api_Should_Return_BadRequest_For_Invalid_Json()
    {
        using var content = new StringContent(
            "{\"userId\":\"invalid-json\",\"operationId\":",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/v1/balances/adjustments", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await ReadErrorsAsync(response);
        errors.GetProperty("title").GetString().Should().Be("Invalid JSON payload");
    }

    private async Task<JsonElement> PostAdjustmentAsync(
        string userId,
        Guid operationId,
        decimal amount)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/balances/adjustments",
            new
            {
                userId,
                operationId,
                amount,
                description = "smoke-test"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadDataAsync(response);
    }

    private static async Task<string> CreateTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/token",
            new
            {
                clientId = "balance-client",
                clientSecret = "balance-secret"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await ReadDataAsync(response);
        return data.GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> ReadErrorsAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("errors").Clone();
    }

    private static async Task<JsonElement> ReadFirstErrorAsync(HttpResponseMessage response)
    {
        var errors = await ReadErrorsAsync(response);
        return errors[0].Clone();
    }

    private static void SetEnvironmentConfiguration(string connectionString)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__PostgresWrite", connectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__PostgresRead", connectionString);
        Environment.SetEnvironmentVariable("DatabaseSettings__RunMigrationsOnStartup", "true");
        Environment.SetEnvironmentVariable("ApiCors__AllowedOrigins", "*");
        Environment.SetEnvironmentVariable("Kestrel__Port", "0");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "BalanceControl");
        Environment.SetEnvironmentVariable("Jwt__Audience", "BalanceControl.Api");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "balance-control-local-signing-key-32-bytes-minimum");
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__ClientId", "balance-client");
        Environment.SetEnvironmentVariable("Jwt__ClientSecret", "balance-secret");
    }

    private static void ClearEnvironmentConfiguration()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__PostgresWrite", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__PostgresRead", null);
        Environment.SetEnvironmentVariable("DatabaseSettings__RunMigrationsOnStartup", null);
        Environment.SetEnvironmentVariable("ApiCors__AllowedOrigins", null);
        Environment.SetEnvironmentVariable("Kestrel__Port", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", null);
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", null);
        Environment.SetEnvironmentVariable("Jwt__ClientId", null);
        Environment.SetEnvironmentVariable("Jwt__ClientSecret", null);
    }

    private sealed class BalanceSmokeWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.Sources.Clear();
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PostgresWrite"] = connectionString,
                    ["ConnectionStrings:PostgresRead"] = connectionString,
                    ["DatabaseSettings:RunMigrationsOnStartup"] = "true",
                    ["ApiCors:AllowedOrigins"] = "*",
                    ["Kestrel:Port"] = "0",
                    ["Jwt:Issuer"] = "BalanceControl",
                    ["Jwt:Audience"] = "BalanceControl.Api",
                    ["Jwt:SigningKey"] = "balance-control-local-signing-key-32-bytes-minimum",
                    ["Jwt:ExpirationMinutes"] = "60",
                    ["Jwt:ClientId"] = "balance-client",
                    ["Jwt:ClientSecret"] = "balance-secret"
                });
            });
        }
    }
}
