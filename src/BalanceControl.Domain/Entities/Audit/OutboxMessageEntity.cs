namespace BalanceControl.Domain.Entities.Audit;

public class OutboxMessageEntity
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
