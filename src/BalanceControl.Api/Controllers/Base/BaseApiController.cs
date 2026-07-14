using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using BalanceControl.Api.Configuration;
using BalanceControl.Api.Controllers.Base.BaseApiResponse;

namespace BalanceControl.Api.Controllers.Base;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Consumes("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class BaseApiController : ControllerBase
{
    protected async Task<IActionResult> ExecuteTaskAsync<T>(
        Func<CancellationToken, Task<T>> execute,
        CancellationToken cancellationToken,
        Func<T, IActionResult>? buildSuccessResponse = null)
    {
        using CancellationTokenSource timeoutCts = CreateRequestTimeout();
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            T result = await execute(linkedCts.Token);
            return buildSuccessResponse?.Invoke(result)
                ?? Ok(ApiResponse<T>.Success(result));
        }
        catch (OperationCanceledException)
        {
            return BuildTimeoutResponse<T>();
        }
    }

    private ObjectResult BuildTimeoutResponse<T>()
        => StatusCode(408, ApiResponse<T>.SingleFailure(408, "Request timeout."));

    private CancellationTokenSource CreateRequestTimeout()
    {
        IConfiguration configuration = HttpContext.RequestServices
            .GetRequiredService<IConfiguration>();
        int timeoutSeconds = ApiProtectionOptions.GetRequestTimeoutSeconds(configuration);

        return new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
    }
}
