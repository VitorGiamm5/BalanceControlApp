using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using BalanceControl.Infrastructure.Database.Metrics;

namespace BalanceControl.Infrastructure.Database.Interceptors;

public class ResilienceInterceptor : DbCommandInterceptor
{
    public static readonly ResiliencePropertyKey<string> OperationKey =
        new("balancecontrol.db.operation");

    private readonly ILogger<ResilienceInterceptor> _logger;
    private readonly ResiliencePipeline _pipeline;

    public ResilienceInterceptor(
        ILogger<ResilienceInterceptor> logger,
        ResiliencePipeline pipeline)
    {
        _logger = logger;
        _pipeline = pipeline;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ResilienceContext ctx = ResilienceContextPool.Shared.Get(cancellationToken);
        ctx.Properties.Set(OperationKey, GetSqlOperation(command.CommandText));
        try
        {
            await _pipeline.ExecuteAsync(
                async context => await base.ReaderExecutingAsync(command, eventData, result, context.CancellationToken),
                ctx);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(ctx);
        }

        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        ResilienceContext ctx = ResilienceContextPool.Shared.Get(cancellationToken);
        ctx.Properties.Set(OperationKey, GetSqlOperation(command.CommandText));
        try
        {
            await _pipeline.ExecuteAsync(
                async context => await base.ScalarExecutingAsync(command, eventData, result, context.CancellationToken),
                ctx);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(ctx);
        }

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ResilienceContext ctx = ResilienceContextPool.Shared.Get(cancellationToken);
        ctx.Properties.Set(OperationKey, GetSqlOperation(command.CommandText));
        try
        {
            await _pipeline.ExecuteAsync(
                async context => await base.NonQueryExecutingAsync(command, eventData, result, context.CancellationToken),
                ctx);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(ctx);
        }

        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        ObserveDatabaseCommand(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        ObserveDatabaseCommand(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ObserveDatabaseCommand(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    internal static bool IsTransient(NpgsqlException ex) => ex.IsTransient || ex.SqlState is
        "08000" or
        "08003" or
        "08006" or
        "08001" or
        "08004" or
        "57P01" or
        "57P02" or
        "57P03";

    private static void ObserveDatabaseCommand(DbCommand command, CommandExecutedEventData eventData)
    {
        string operation = GetSqlOperation(command.CommandText);
        string context = eventData.Context?.GetType().Name ?? "unknown";

        DatabaseMetrics.CommandDurationSeconds
            .WithLabels(operation, context)
            .Observe(eventData.Duration.TotalSeconds);

        if (IsReadOperation(operation))
            DatabaseMetrics.ReadOperationsTotal.WithLabels(operation, context).Inc();
        else if (IsWriteOperation(operation))
            DatabaseMetrics.WriteOperationsTotal.WithLabels(operation, context).Inc();
    }

    internal static string GetSqlOperation(string commandText)
    {
        string sql = commandText.TrimStart();
        if (string.IsNullOrWhiteSpace(sql))
            return "unknown";

        int endIndex = 0;
        while (endIndex < sql.Length && !char.IsWhiteSpace(sql[endIndex]))
            endIndex++;

        return sql[..endIndex].ToLowerInvariant();
    }

    private static bool IsReadOperation(string operation) => operation is "select";

    private static bool IsWriteOperation(string operation) => operation is
        "insert" or
        "update" or
        "delete" or
        "merge";
}
