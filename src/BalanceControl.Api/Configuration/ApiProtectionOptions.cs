using Microsoft.Extensions.Configuration;

namespace BalanceControl.Api.Configuration;

public sealed class ApiProtectionOptions
{
    private const int DefaultRequestTimeoutSeconds = 3;
    private const int DefaultRequestHeadersTimeoutSeconds = 15;
    private const int DefaultKeepAliveTimeoutSeconds = 30;
    private const int DefaultKestrelPort = 5000;

    public static int GetKestrelPort(IConfiguration configuration)
        => GetPositiveInt(
            configuration,
            "Kestrel:Port",
            "API_PORT",
            DefaultKestrelPort);

    public static int GetRequestTimeoutSeconds(IConfiguration configuration)
        => GetPositiveInt(
            configuration,
            "ApiProtection:RequestTimeoutSeconds",
            "RequestTimeoutSeconds",
            DefaultRequestTimeoutSeconds);

    public static int GetRequestHeadersTimeoutSeconds(IConfiguration configuration)
        => GetPositiveInt(
            configuration,
            "ApiProtection:RequestHeadersTimeoutSeconds",
            "RequestHeadersTimeoutSeconds",
            DefaultRequestHeadersTimeoutSeconds);

    public static int GetKeepAliveTimeoutSeconds(IConfiguration configuration)
        => GetPositiveInt(
            configuration,
            "ApiProtection:KeepAliveTimeoutSeconds",
            "KeepAliveTimeoutSeconds",
            DefaultKeepAliveTimeoutSeconds);

    private static int GetPositiveInt(
        IConfiguration configuration,
        string key,
        string legacyKey,
        int defaultValue)
    {
        int? value = configuration.GetValue<int?>(key)
            ?? configuration.GetValue<int?>(legacyKey);

        return value is > 0
            ? value.Value
            : defaultValue;
    }
}
