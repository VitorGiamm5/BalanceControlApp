using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using BalanceControl.Api.Configuration;

namespace BalanceControl.Api.Services.Auth;

public sealed class SimpleJwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public TokenResponse? TryCreateToken(TokenRequest request)
    {
        if (!string.Equals(request.ClientId, _options.ClientId, StringComparison.Ordinal) ||
            !string.Equals(request.ClientSecret, _options.ClientSecret, StringComparison.Ordinal))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.ExpirationMinutes);
        var credentials = new SigningCredentials(_options.GetSigningKey(), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, request.ClientId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("scope", "balances:read balances:write")
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            TokenType = "Bearer",
            ExpiresAt = expiresAt
        };
    }
}
