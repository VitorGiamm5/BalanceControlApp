using FluentValidation;
using BalanceControl.Domain.Repositories.Balances;
using BalanceControl.Domain.Services.Balances.Business;
using BalanceControl.Domain.Services.Balances.Dtos;
using BalanceControl.Domain.Services.Base.Dtos;

namespace BalanceControl.Application.Business.Balances;

public sealed class BalanceService(
    IBalanceRepository repository,
    IValidator<AdjustBalanceRequest> validator)
    : IBalanceService
{
    public async Task<BalanceAdjustmentResult> AdjustAsync(
        AdjustBalanceRequest request,
        CancellationToken cancellationToken)
    {
        request.UserId = request.UserId.Trim();
        request.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        return await repository.AdjustAsync(
            request,
            BalanceRequestHasher.Compute(request),
            cancellationToken);
    }

    public Task<BalanceResponse?> GetBalanceAsync(
        string userId,
        CancellationToken cancellationToken)
        => repository.GetBalanceAsync(userId.Trim(), cancellationToken);

    public Task<PagedResult<BalanceMovementResponse>?> GetStatementAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
        => repository.GetStatementAsync(
            userId.Trim(),
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 200),
            cancellationToken);
}
