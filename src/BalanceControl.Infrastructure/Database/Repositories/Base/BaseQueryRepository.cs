using Microsoft.EntityFrameworkCore;
using BalanceControl.Domain.Exceptions;
using BalanceControl.Domain.Repositories.Base;
using BalanceControl.Infrastructure.Database.Contexts;

namespace BalanceControl.Infrastructure.Database.Repositories.Base;

public abstract class BaseQueryRepository<T>(ReadOnlyDbContext context) : IBaseQueryRepository<T> where T : class
{
    protected readonly ReadOnlyDbContext _context = context;

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.Set<T>()
            .ToListAsync(cancellationToken);
    }

    public async Task<T> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            throw new ArgumentException("Id inválido.", nameof(id));

        return await _context.Set<T>()
            .FirstOrDefaultAsync(e => EF.Property<long>(e, "Id") == id, cancellationToken)
            ?? throw new NotFoundException(typeof(T).Name, id);
    }
}
