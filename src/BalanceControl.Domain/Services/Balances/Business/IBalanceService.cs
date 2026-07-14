using BalanceControl.Domain.Services.Balances.Dtos;
using BalanceControl.Domain.Services.Base.Dtos;

namespace BalanceControl.Domain.Services.Balances.Business;

public interface IBalanceService
{
    Task<BalanceAdjustmentResult> AdjustAsync(
        AdjustBalanceRequest request,
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
