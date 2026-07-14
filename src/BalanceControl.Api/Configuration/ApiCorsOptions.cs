namespace BalanceControl.Api.Configuration;

public sealed class ApiCorsOptions
{
    public const string SectionName = "ApiCors";
    public const string PolicyName = "BalanceControlCors";

    public string AllowedOrigins { get; init; } = "*";

    public string[] GetAllowedOrigins()
        => AllowedOrigins
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public bool AllowsAnyOrigin()
        => GetAllowedOrigins().Any(origin => origin == "*");
}
