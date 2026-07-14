namespace BalanceControl.Domain.Repositories.Base;

public interface IBaseQueryRepository<TEntity> : IBaseRepository where TEntity : class
{
    Task<TEntity> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken);
}
