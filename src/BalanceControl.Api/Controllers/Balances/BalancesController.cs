using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BalanceControl.Api.Controllers.Base;
using BalanceControl.Api.Controllers.Base.BaseApiResponse;
using BalanceControl.Domain.Services.Balances.Business;
using BalanceControl.Domain.Services.Balances.Dtos;
using BalanceControl.Domain.Services.Base.Dtos;

namespace BalanceControl.Api.Controllers.Balances;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/balances")]
[Authorize]
public sealed class BalancesController(IBalanceService balanceService) : BaseApiController
{
    [HttpPost("adjustments")]
    [ProducesResponseType(typeof(ApiResponse<BalanceAdjustmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Adjust(
        [FromBody] AdjustBalanceRequest request,
        CancellationToken cancellationToken)
        => ExecuteTaskAsync(
            token => balanceService.AdjustAsync(request, token),
            cancellationToken,
            result => result.Status == BalanceAdjustmentResultStatus.Conflict
                ? Conflict(ApiResponse<object>.SingleFailure(409, result.ConflictMessage!))
                : Ok(ApiResponse<BalanceAdjustmentResponse>.Success(result.Response!)));

    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(ApiResponse<BalanceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetBalance(
        string userId,
        CancellationToken cancellationToken)
        => ExecuteTaskAsync(
            token => balanceService.GetBalanceAsync(userId, token),
            cancellationToken,
            result => result is null
                ? NotFound(ApiResponse<object>.SingleFailure(404, "Usuario sem saldo registrado."))
                : Ok(ApiResponse<BalanceResponse>.Success(result)));

    [HttpGet("{userId}/statement")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BalanceMovementResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetStatement(
        string userId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
        => ExecuteTaskAsync(
            token => balanceService.GetStatementAsync(
                userId,
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 50 : pageSize,
                token),
            cancellationToken,
            result => result is null
                ? NotFound(ApiResponse<object>.SingleFailure(404, "Usuario sem saldo registrado."))
                : Ok(ApiResponse<object>.Success(result)));
}
