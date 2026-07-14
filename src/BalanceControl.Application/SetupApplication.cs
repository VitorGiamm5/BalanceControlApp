using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BalanceControl.Application.Business.Balances;
using BalanceControl.Domain.Services.Balances.Business;

namespace BalanceControl.Application;

public static class SetupApplication
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(SetupApplication).Assembly);
        services.AddScoped<IBalanceService, BalanceService>();

        return services;
    }
}
