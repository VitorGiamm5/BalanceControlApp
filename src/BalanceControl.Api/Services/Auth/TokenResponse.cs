namespace BalanceControl.Api.Services.Auth;

public sealed class TokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public DateTime ExpiresAt { get; init; }
}
