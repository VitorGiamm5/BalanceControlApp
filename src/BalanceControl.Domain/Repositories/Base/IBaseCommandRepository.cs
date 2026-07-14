namespace BalanceControl.Domain.Repositories.Base;

public interface IBaseCommandRepository<TEntity> : IBaseRepository where TEntity : class
{
    Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken);
    Task<TEntity> UpdateAsync(TEntity entity, long id, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);
}
