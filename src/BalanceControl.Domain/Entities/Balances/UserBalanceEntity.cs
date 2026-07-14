namespace BalanceControl.Domain.Entities.Balances;

public sealed class UserBalanceEntity
{
    public string UserId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public long Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
