namespace BalanceControl.Domain.Entities.Audit;

public class AuditLogEntity
{
    public long Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string UserId { get; set; } = "system";
    public string UserName { get; set; } = "system";
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; }
}
