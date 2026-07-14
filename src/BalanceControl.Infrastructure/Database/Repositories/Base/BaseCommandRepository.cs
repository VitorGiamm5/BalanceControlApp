using BalanceControl.Domain.Exceptions;
using BalanceControl.Domain.Repositories.Base;
using BalanceControl.Infrastructure.Database.Contexts;

namespace BalanceControl.Infrastructure.Database.Repositories.Base;

public abstract class BaseCommandRepository<T>(ApplicationDbContext context) : IBaseCommandRepository<T> where T : class
{

    public readonly ApplicationDbContext _context = context;

    public virtual async Task<T> CreateAsync(T entity, CancellationToken cancellationToken)
    {
        await _context.Set<T>().AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public virtual async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var entity = await _context.Set<T>()
            .FindAsync([id], cancellationToken)
            ?? throw new NotFoundException(typeof(T).Name, id);

        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public virtual async Task<T> UpdateAsync(T entity, long id, CancellationToken cancellationToken)
    {
        _context.Set<T>().Update(entity);

        await _context.SaveChangesAsync(cancellationToken);

        return entity;
    }
}
