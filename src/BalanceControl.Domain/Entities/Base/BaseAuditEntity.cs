namespace BalanceControl.Domain.Entities.Base;

public class BaseAuditEntity
{
    public DateTime? CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime? DeletedDate { get; set; }
    public long? CreatedByUserId { get; set; }
    public long? UpdatedByUserId { get; set; }
    public long? DeletedByUserId { get; set; }
    public bool? IsDeleted { get; set; }
    public bool? IsActive { get; set; }
}
