namespace BalanceControl.Domain.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entityName, long id)
        : base($"{entityName} com id {id} não encontrado.") { }
}
