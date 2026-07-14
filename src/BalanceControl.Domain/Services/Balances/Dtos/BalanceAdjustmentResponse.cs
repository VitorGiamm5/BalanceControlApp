namespace BalanceControl.Domain.Services.Balances.Dtos;

public sealed class BalanceAdjustmentResponse
{
    public required string UserId { get; init; }
    public required Guid OperationId { get; init; }
    public required Guid MovementId { get; init; }
    public required decimal Amount { get; init; }
    public required decimal Balance { get; init; }
    public required bool Applied { get; init; }
    public required DateTime CreatedAt { get; init; }
}
