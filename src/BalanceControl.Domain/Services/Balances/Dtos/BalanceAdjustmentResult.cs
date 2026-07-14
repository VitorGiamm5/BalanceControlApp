namespace BalanceControl.Domain.Services.Balances.Dtos;

public sealed class BalanceAdjustmentResult
{
    private BalanceAdjustmentResult(
        BalanceAdjustmentResultStatus status,
        BalanceAdjustmentResponse? response,
        string? conflictMessage)
    {
        Status = status;
        Response = response;
        ConflictMessage = conflictMessage;
    }

    public BalanceAdjustmentResultStatus Status { get; }
    public BalanceAdjustmentResponse? Response { get; }
    public string? ConflictMessage { get; }

    public static BalanceAdjustmentResult Applied(BalanceAdjustmentResponse response)
        => new(BalanceAdjustmentResultStatus.Applied, response, null);

    public static BalanceAdjustmentResult Replayed(BalanceAdjustmentResponse response)
        => new(BalanceAdjustmentResultStatus.Replayed, response, null);

    public static BalanceAdjustmentResult Conflict(string message)
        => new(BalanceAdjustmentResultStatus.Conflict, null, message);
}

public enum BalanceAdjustmentResultStatus
{
    Applied,
    Replayed,
    Conflict
}
