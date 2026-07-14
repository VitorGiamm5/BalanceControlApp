using BalanceControl.Domain.Security;

namespace BalanceControl.Infrastructure.Database.Contexts;

internal sealed class SystemCurrentUserContext : ICurrentUserContext
{
    public string UserId => "system";
    public string UserName => "system";
    public string? CorrelationId => null;
}
