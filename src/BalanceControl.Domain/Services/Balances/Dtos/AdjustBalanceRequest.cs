namespace BalanceControl.Domain.Services.Balances.Dtos;

public sealed class AdjustBalanceRequest
{
    public string UserId { get; set; } = string.Empty;
    public Guid OperationId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? OccurredAt { get; set; }
    public string? Description { get; set; }
}
