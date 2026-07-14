namespace BalanceControl.Domain.Entities.Balances;

public sealed class BalanceMovementEntity
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid OperationId { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Description { get; set; }
}
