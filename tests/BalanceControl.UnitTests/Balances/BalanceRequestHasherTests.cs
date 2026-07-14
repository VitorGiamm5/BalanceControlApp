using FluentAssertions;
using BalanceControl.Application.Business.Balances;
using BalanceControl.Domain.Services.Balances.Dtos;

namespace BalanceControl.UnitTests.Balances;

public class BalanceRequestHasherTests
{
    [Fact]
    public void Compute_Should_Ignore_Outer_Whitespace_When_Hashing()
    {
        var operationId = Guid.NewGuid();
        var trimmed = BuildRequest(operationId, "user-0001", "initial credit");
        var padded = BuildRequest(operationId, " user-0001 ", " initial credit ");

        var firstHash = BalanceRequestHasher.Compute(trimmed);
        var secondHash = BalanceRequestHasher.Compute(padded);

        secondHash.Should().Be(firstHash);
    }

    private static AdjustBalanceRequest BuildRequest(Guid operationId, string userId, string description)
        => new()
        {
            UserId = userId,
            OperationId = operationId,
            Amount = 100.50m,
            OccurredAt = new DateTime(2026, 7, 13, 18, 0, 0, DateTimeKind.Utc),
            Description = description
        };
}
