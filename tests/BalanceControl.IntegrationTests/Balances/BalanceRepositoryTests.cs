using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using BalanceControl.Domain.Entities.Balances;
using BalanceControl.Domain.Services.Balances.Dtos;
using BalanceControl.Infrastructure.Database.Contexts;
using BalanceControl.Infrastructure.Database.Repositories.Balances;
using BalanceControl.IntegrationTests.Support;

namespace BalanceControl.IntegrationTests.Balances;

public class BalanceRepositoryTests(PostgresDatabaseFixture db) : IClassFixture<PostgresDatabaseFixture>, IAsyncLifetime
{
    private DbContextOptions<ApplicationDbContext> _options = null!;
    private ApplicationDbContext _ctx = null!;
    private BalanceRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(db.ConnectionString)
            .UseLoggerFactory(NullLoggerFactory.Instance)
            .Options;

        _ctx = new ApplicationDbContext(_options);
        await _ctx.Database.MigrateAsync();
        await db.ResetDatabaseAsync();
        _repo = new BalanceRepository(_ctx);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public async Task AdjustAsync_Should_Create_User_Balance_And_Movement()
    {
        var request = BuildRequest("user-1", 125.50m);

        var result = await _repo.AdjustAsync(request, Hash("A"), CancellationToken.None);

        result.Status.Should().Be(BalanceAdjustmentResultStatus.Applied);
        result.Response!.Applied.Should().BeTrue();
        result.Response.Balance.Should().Be(125.50m);

        var balance = await _repo.GetBalanceAsync("user-1", CancellationToken.None);
        balance.Should().NotBeNull();
        balance!.Balance.Should().Be(125.50m);
        balance.Version.Should().Be(1);

        var movements = await _repo.GetStatementAsync("user-1", 1, 10, CancellationToken.None);
        movements!.Items.Should().ContainSingle()
            .Which.BalanceAfter.Should().Be(125.50m);
    }

    [Fact]
    public async Task AdjustAsync_Should_Not_Apply_Replayed_Operation_Twice()
    {
        var request = BuildRequest("user-2", 40m);
        var hash = Hash("B");

        var first = await _repo.AdjustAsync(request, hash, CancellationToken.None);
        var replay = await _repo.AdjustAsync(request, hash, CancellationToken.None);

        first.Status.Should().Be(BalanceAdjustmentResultStatus.Applied);
        replay.Status.Should().Be(BalanceAdjustmentResultStatus.Replayed);
        replay.Response!.Applied.Should().BeFalse();
        replay.Response.Balance.Should().Be(40m);

        var balance = await _repo.GetBalanceAsync("user-2", CancellationToken.None);
        balance!.Balance.Should().Be(40m);

        var movements = await _ctx.Set<BalanceMovementEntity>()
            .Where(movement => movement.UserId == "user-2")
            .ToArrayAsync();
        movements.Should().ContainSingle();
    }

    [Fact]
    public async Task AdjustAsync_Should_Reject_Same_Operation_With_Different_Payload()
    {
        var request = BuildRequest("user-3", 10m);

        await _repo.AdjustAsync(request, Hash("C"), CancellationToken.None);
        var result = await _repo.AdjustAsync(request, Hash("D"), CancellationToken.None);

        result.Status.Should().Be(BalanceAdjustmentResultStatus.Conflict);
        result.ConflictMessage.Should().Contain("payload diferente");
    }

    [Fact]
    public async Task AdjustAsync_Should_Keep_Balance_Consistent_For_Concurrent_Updates()
    {
        var userId = "user-concurrent";

        var tasks = Enumerable.Range(1, 25)
            .Select(index => AdjustWithNewContextAsync(
                BuildRequest(userId, 1m, Guid.NewGuid()),
                Hash(index.ToString("D2"))))
            .ToArray();

        await Task.WhenAll(tasks);

        var balance = await _repo.GetBalanceAsync(userId, CancellationToken.None);
        balance!.Balance.Should().Be(25m);
        balance.Version.Should().Be(25);
    }

    [Fact]
    public async Task AdjustAsync_Should_Apply_Concurrent_Replays_Only_Once()
    {
        var userId = "user-concurrent-replay";
        var operationId = Guid.NewGuid();
        var request = BuildRequest(userId, 7m, operationId);
        var hash = Hash("E");

        var tasks = Enumerable.Range(1, 10)
            .Select(_ => AdjustWithNewContextAsync(request, hash))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Count(result => result.Status == BalanceAdjustmentResultStatus.Applied)
            .Should().Be(1);
        results.Count(result => result.Status == BalanceAdjustmentResultStatus.Replayed)
            .Should().Be(9);

        var balance = await _repo.GetBalanceAsync(userId, CancellationToken.None);
        balance!.Balance.Should().Be(7m);

        var movementCount = await _ctx.Set<BalanceMovementEntity>()
            .CountAsync(movement => movement.UserId == userId);
        movementCount.Should().Be(1);
    }

    private async Task<BalanceAdjustmentResult> AdjustWithNewContextAsync(
        AdjustBalanceRequest request,
        string hash)
    {
        await using var context = new ApplicationDbContext(_options);
        var repository = new BalanceRepository(context);

        return await repository.AdjustAsync(request, hash, CancellationToken.None);
    }

    private static AdjustBalanceRequest BuildRequest(
        string userId,
        decimal amount,
        Guid? operationId = null)
        => new()
        {
            UserId = userId,
            OperationId = operationId ?? Guid.NewGuid(),
            Amount = amount,
            OccurredAt = DateTime.UtcNow,
            Description = "integration-test"
        };

    private static string Hash(string value)
        => value.PadRight(64, '0')[..64];
}
