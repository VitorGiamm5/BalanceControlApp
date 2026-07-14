namespace BalanceControl.Domain.Services.Base.Dtos;

[Serializable]
public sealed class BooleanResponse
{
    public bool IsSuccess { get; set; } = false;
}
