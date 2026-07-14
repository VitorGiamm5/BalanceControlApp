namespace BalanceControl.Domain.Services.Base;

/// <summary>
/// Business layer contract with Inbox/Outbox pipeline.
/// </summary>
public interface IBaseBusinessService<TInObject, TOutObject>
    where TInObject : class
    where TOutObject : class
{
    Task<TOutObject> ExecuteAsync(TInObject input, CancellationToken ct = default);
}
