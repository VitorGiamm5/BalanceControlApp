namespace BalanceControl.Api.Services.Auth;

public sealed class TokenRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
