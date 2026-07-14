using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BalanceControl.Api.Controllers.Base;
using BalanceControl.Api.Controllers.Base.BaseApiResponse;
using BalanceControl.Api.Services.Auth;

namespace BalanceControl.Api.Controllers.Auth;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
public sealed class AuthController(SimpleJwtTokenService tokenService) : BaseApiController
{
    [HttpPost("token")]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public IActionResult CreateToken([FromBody] TokenRequest request)
    {
        var response = tokenService.TryCreateToken(request);

        return response is null
            ? Unauthorized(ApiResponse<object>.SingleFailure(401, "Credenciais invalidas."))
            : Ok(ApiResponse<TokenResponse>.Success(response));
    }
}
