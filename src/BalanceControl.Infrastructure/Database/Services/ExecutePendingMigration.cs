using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using BalanceControl.Infrastructure.Database.Contexts;

namespace BalanceControl.Infrastructure.Database.Services;

public static class ExecutePendingMigration
{
    public static async Task ExecuteAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ExecutePendingMigration).FullName!);

        var retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 10,
                Delay = TimeSpan.FromSeconds(5),
                BackoffType = DelayBackoffType.Constant,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "[Migration] Tentativa {AttemptNumber} aguardando banco subir. Proxima tentativa em {RetryDelay}.",
                        args.AttemptNumber + 1,
                        args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        try
        {
            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await retryPipeline.ExecuteAsync(async ct =>
            {
                logger.LogInformation("[Migration] Validating if there are pending migrations...");

                var migrations = await dbContext.Database.GetPendingMigrationsAsync(ct);

                if (migrations.Any())
                {
                    await dbContext.Database.MigrateAsync(ct);

                    logger.LogInformation("[Migration] Applied migrations: {Migrations}", migrations);
                }
                else
                {
                    logger.LogInformation("[Migration] Has no pending migrations.");
                }

            });

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error to apply migrations.");

            throw;
        }
    }
}
