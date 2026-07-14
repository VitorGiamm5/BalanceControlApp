namespace BalanceControl.Domain.Services.Balances.Dtos;

public sealed class BalanceResponse
{
    public required string UserId { get; init; }
    public required decimal Balance { get; init; }
    public required long Version { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
