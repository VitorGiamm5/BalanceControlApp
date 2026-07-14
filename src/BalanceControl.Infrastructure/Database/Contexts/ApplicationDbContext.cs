using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using BalanceControl.Domain.Entities.Audit;
using BalanceControl.Domain.Security;
using BalanceControl.Infrastructure.Database.Converters;

namespace BalanceControl.Infrastructure.Database.Contexts;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserContext _currentUserContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : this(options, new SystemCurrentUserContext())
    {
    }

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserContext currentUserContext) : base(options)
    {
        _currentUserContext = currentUserContext;
    }

    protected ApplicationDbContext(
        DbContextOptions options,
        ICurrentUserContext currentUserContext) : base(options)
    {
        _currentUserContext = currentUserContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("balance_control");
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        var dateConverter         = new DateTimeUnspecifiedConverter();
        var nullableDateConverter = new NullableDateTimeUnspecifiedConverter();

        // Aplica apenas em colunas com HasColumnType("timestamp") — que é timestamp without time zone no PostgreSQL.
        // Colunas sem HasColumnType() explícito ficam com o default Npgsql (timestamptz = timestamp with time zone),
        // que requer Kind=UTC, e NÃO devem receber este converter.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property is not IMutableProperty mutableProp || property.GetValueConverter() is not null)
                    continue;

                var columnType = property.GetColumnType();
                if (columnType is not ("timestamp" or "timestamp without time zone"))
                    continue;

                if (property.ClrType == typeof(DateTime))
                    mutableProp.SetValueConverter(dateConverter);
                else if (property.ClrType == typeof(DateTime?))
                    mutableProp.SetValueConverter(nullableDateConverter);
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        AddAuditAndOutboxEntries();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddAuditAndOutboxEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AddAuditAndOutboxEntries();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AddAuditAndOutboxEntries();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AddAuditAndOutboxEntries()
    {
        ChangeTracker.DetectChanges();

        var now = DateTime.UtcNow;
        var entries = ChangeTracker
            .Entries()
            .Where(entry =>
                entry.Entity is not AuditLogEntity &&
                entry.Entity is not OutboxMessageEntity &&
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();

        foreach (var entry in entries)
        {
            var auditLog = CreateAuditLog(entry, now);
            var outboxMessage = CreateOutboxMessage(auditLog, now);

            Set<AuditLogEntity>().Add(auditLog);
            Set<OutboxMessageEntity>().Add(outboxMessage);
        }
    }

    private AuditLogEntity CreateAuditLog(EntityEntry entry, DateTime now)
    {
        return new AuditLogEntity
        {
            EntityName = entry.Metadata.ClrType.Name,
            EntityKey = GetEntityKey(entry),
            Action = entry.State.ToString(),
            OldValues = entry.State is EntityState.Modified or EntityState.Deleted
                ? SerializeValues(entry.Properties, useOriginalValues: true)
                : null,
            NewValues = entry.State is EntityState.Added or EntityState.Modified
                ? SerializeValues(entry.Properties, useOriginalValues: false)
                : null,
            UserId = _currentUserContext.UserId,
            UserName = _currentUserContext.UserName,
            CorrelationId = _currentUserContext.CorrelationId,
            CreatedAt = now
        };
    }

    private static OutboxMessageEntity CreateOutboxMessage(AuditLogEntity auditLog, DateTime now)
    {
        var payload = JsonSerializer.Serialize(new
        {
            auditLog.EntityName,
            auditLog.EntityKey,
            auditLog.Action,
            auditLog.UserId,
            auditLog.UserName,
            auditLog.CorrelationId,
            OccurredAt = now
        });

        return new OutboxMessageEntity
        {
            EventType = $"{auditLog.EntityName}.{auditLog.Action}",
            Payload = payload,
            Status = "Pending",
            OccurredAt = now
        };
    }

    private static string GetEntityKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();

        if (key is null)
            return string.Empty;

        var values = key.Properties
            .Select(property => $"{property.Name}:{entry.Property(property.Name).CurrentValue}");

        return string.Join("|", values);
    }

    private static string SerializeValues(IEnumerable<PropertyEntry> properties, bool useOriginalValues)
    {
        var values = properties
            .Where(property => !property.Metadata.IsShadowProperty())
            .Where(property => property.Metadata.GetBeforeSaveBehavior() != PropertySaveBehavior.Ignore)
            .ToDictionary(
                property => property.Metadata.Name,
                property => useOriginalValues ? property.OriginalValue : property.CurrentValue);

        return JsonSerializer.Serialize(values);
    }
}
