using BalanceControl.Domain.Services.Balances.Dtos;
using BalanceControl.Domain.Services.Base.Dtos;

namespace BalanceControl.Domain.Repositories.Balances;

public interface IBalanceRepository
{
    Task<BalanceAdjustmentResult> AdjustAsync(
        AdjustBalanceRequest request,
        string requestHash,
        CancellationToken cancellationToken);

    Task<BalanceResponse?> GetBalanceAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<PagedResult<BalanceMovementResponse>?> GetStatementAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
