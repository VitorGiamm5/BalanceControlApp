namespace BalanceControl.Domain.Security;

public interface ICurrentUserContext
{
    string UserId { get; }
    string UserName { get; }
    string? CorrelationId { get; }
}
