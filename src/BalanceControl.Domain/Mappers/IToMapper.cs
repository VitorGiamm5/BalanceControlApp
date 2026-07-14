namespace BalanceControl.Domain.Mappers;

public interface IToMapper<out TInput> where TInput : class
{
    TInput MapTo();
}
