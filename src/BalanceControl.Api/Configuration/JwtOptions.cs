using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BalanceControl.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "BalanceControl";
    public string Audience { get; init; } = "BalanceControl.Api";
    public string SigningKey { get; init; } = string.Empty;
    public int ExpirationMinutes { get; init; } = 60;
    public string ClientId { get; init; } = "balance-client";
    public string ClientSecret { get; init; } = string.Empty;

    public SymmetricSecurityKey GetSigningKey()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 bytes.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException("Jwt:Issuer is required.");

        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Jwt:Audience is required.");

        if (ExpirationMinutes < 1)
            throw new InvalidOperationException("Jwt:ExpirationMinutes must be greater than zero.");

        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("Jwt:ClientId is required.");

        if (string.IsNullOrWhiteSpace(ClientSecret))
            throw new InvalidOperationException("Jwt:ClientSecret is required.");

        _ = GetSigningKey();
    }
}
