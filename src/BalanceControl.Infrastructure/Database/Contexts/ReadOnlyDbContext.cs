using Microsoft.EntityFrameworkCore;
using BalanceControl.Domain.Security;

namespace BalanceControl.Infrastructure.Database.Contexts;

// Contexto dedicado para leitura — aponta para a réplica
public class ReadOnlyDbContext(
    DbContextOptions<ReadOnlyDbContext> options,
    ICurrentUserContext currentUserContext) : ApplicationDbContext(options, currentUserContext)
{
    public override int SaveChanges()
        => throw new InvalidOperationException("ReadOnlyDbContext não permite escrita.");

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new InvalidOperationException("ReadOnlyDbContext não permite escrita.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("ReadOnlyDbContext não permite escrita.");

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("ReadOnlyDbContext não permite escrita.");
}
