using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using BalanceControl.Domain.Entities.Balances;
using BalanceControl.Domain.Repositories.Balances;
using BalanceControl.Domain.Services.Balances.Dtos;
using BalanceControl.Domain.Services.Base.Dtos;
using BalanceControl.Infrastructure.Database.Contexts;

namespace BalanceControl.Infrastructure.Database.Repositories.Balances;

public sealed class BalanceRepository(ApplicationDbContext context) : IBalanceRepository
{
    private const string Schema = "\"balance_control\"";
    private const string DuplicateKeySqlState = "23505";
    private readonly ApplicationDbContext _context = context;

    public async Task<BalanceAdjustmentResult> AdjustAsync(
        AdjustBalanceRequest request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await GetMovementAsync(request.UserId, request.OperationId, cancellationToken);
        if (existing is not null)
            return BuildReplayResult(existing, requestHash);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var now = DateTime.UtcNow;
            var occurredAt = request.OccurredAt?.ToUniversalTime() ?? now;
            var movementId = Guid.NewGuid();

            var balance = await UpsertBalanceAsync(
                request.UserId,
                request.Amount,
                now,
                transaction,
                cancellationToken);

            await InsertMovementAsync(
                movementId,
                request,
                requestHash,
                balance,
                occurredAt,
                now,
                transaction,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return BalanceAdjustmentResult.Applied(new BalanceAdjustmentResponse
            {
                UserId = request.UserId,
                OperationId = request.OperationId,
                MovementId = movementId,
                Amount = request.Amount,
                Balance = balance,
                Applied = true,
                CreatedAt = now
            });
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateKeySqlState)
        {
            await transaction.RollbackAsync(cancellationToken);

            var replay = await GetMovementAsync(request.UserId, request.OperationId, cancellationToken);
            if (replay is null)
                throw;

            return BuildReplayResult(replay, requestHash);
        }
    }

    public async Task<BalanceResponse?> GetBalanceAsync(
        string userId,
        CancellationToken cancellationToken)
        => await _context.Set<UserBalanceEntity>()
            .Where(balance => balance.UserId == userId)
            .Select(balance => new BalanceResponse
            {
                UserId = balance.UserId,
                Balance = balance.Balance,
                Version = balance.Version,
                UpdatedAt = balance.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<BalanceMovementResponse>?> GetStatementAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var balanceExists = await _context.Set<UserBalanceEntity>()
            .AnyAsync(balance => balance.UserId == userId, cancellationToken);

        if (!balanceExists)
            return null;

        var query = _context.Set<BalanceMovementEntity>()
            .Where(movement => movement.UserId == userId);

        var totalItems = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(movement => movement.CreatedAt)
            .ThenByDescending(movement => movement.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(movement => new BalanceMovementResponse
            {
                MovementId = movement.Id,
                OperationId = movement.OperationId,
                Amount = movement.Amount,
                BalanceAfter = movement.BalanceAfter,
                OccurredAt = movement.OccurredAt,
                CreatedAt = movement.CreatedAt,
                Description = movement.Description
            })
            .ToArrayAsync(cancellationToken);

        return PagedResult<BalanceMovementResponse>.Create(items, page, pageSize, totalItems);
    }

    private async Task<BalanceMovementEntity?> GetMovementAsync(
        string userId,
        Guid operationId,
        CancellationToken cancellationToken)
        => await _context.Set<BalanceMovementEntity>()
            .SingleOrDefaultAsync(
                movement => movement.UserId == userId && movement.OperationId == operationId,
                cancellationToken);

    private static BalanceAdjustmentResult BuildReplayResult(
        BalanceMovementEntity movement,
        string requestHash)
    {
        if (!string.Equals(movement.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return BalanceAdjustmentResult.Conflict(
                "OperationId ja processado para este usuario com payload diferente.");
        }

        return BalanceAdjustmentResult.Replayed(new BalanceAdjustmentResponse
        {
            UserId = movement.UserId,
            OperationId = movement.OperationId,
            MovementId = movement.Id,
            Amount = movement.Amount,
            Balance = movement.BalanceAfter,
            Applied = false,
            CreatedAt = movement.CreatedAt
        });
    }

    private async Task<decimal> UpsertBalanceAsync(
        string userId,
        decimal amount,
        DateTime now,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(transaction);
        command.CommandText =
            $$"""
            insert into {{Schema}}.tb_user_balance
                (user_id, balance, version, created_at, updated_at)
            values
                (@user_id, @amount, 1, @now, @now)
            on conflict (user_id)
            do update set
                balance = {{Schema}}.tb_user_balance.balance + excluded.balance,
                version = {{Schema}}.tb_user_balance.version + 1,
                updated_at = excluded.updated_at
            returning balance;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("amount", amount);
        command.Parameters.AddWithValue("now", now);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (decimal)result!;
    }

    private async Task InsertMovementAsync(
        Guid movementId,
        AdjustBalanceRequest request,
        string requestHash,
        decimal balanceAfter,
        DateTime occurredAt,
        DateTime createdAt,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(transaction);
        command.CommandText =
            $$"""
            insert into {{Schema}}.tb_balance_movement
                (id, user_id, operation_id, amount, balance_after, request_hash, occurred_at, created_at, description)
            values
                (@id, @user_id, @operation_id, @amount, @balance_after, @request_hash, @occurred_at, @created_at, @description);
            """;
        command.Parameters.AddWithValue("id", movementId);
        command.Parameters.AddWithValue("user_id", request.UserId);
        command.Parameters.AddWithValue("operation_id", request.OperationId);
        command.Parameters.AddWithValue("amount", request.Amount);
        command.Parameters.AddWithValue("balance_after", balanceAfter);
        command.Parameters.AddWithValue("request_hash", requestHash);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        command.Parameters.AddWithValue("created_at", createdAt);
        command.Parameters.AddWithValue("description", (object?)request.Description ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlCommand CreateCommand(IDbContextTransaction transaction)
    {
        var command = _context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        return (NpgsqlCommand)command;
    }
}
