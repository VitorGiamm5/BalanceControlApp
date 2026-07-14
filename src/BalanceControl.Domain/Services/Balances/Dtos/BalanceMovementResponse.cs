namespace BalanceControl.Domain.Services.Balances.Dtos;

public sealed class BalanceMovementResponse
{
    public required Guid MovementId { get; init; }
    public required Guid OperationId { get; init; }
    public required decimal Amount { get; init; }
    public required decimal BalanceAfter { get; init; }
    public required DateTime OccurredAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? Description { get; init; }
}
