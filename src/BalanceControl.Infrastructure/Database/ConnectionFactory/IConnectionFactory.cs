using Npgsql;

namespace BalanceControl.Infrastructure.Database.ConnectionFactory;

public interface IConnectionFactory
{
    NpgsqlConnection CreateWriteConnection();
    NpgsqlConnection CreateReadConnection();
}
