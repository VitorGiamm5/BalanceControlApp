using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BalanceControl.Domain.Entities.Audit;

namespace BalanceControl.Infrastructure.Database.Configuration.Audit;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
    {
        builder.ToTable("tb_audit_log");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();

        builder.Property(e => e.EntityName)
            .HasColumnName("entity_name")
            .HasColumnType("varchar")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.EntityKey)
            .HasColumnName("entity_key")
            .HasColumnType("varchar")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Action)
            .HasColumnName("action")
            .HasColumnType("varchar")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.OldValues)
            .HasColumnName("old_values")
            .HasColumnType("jsonb");

        builder.Property(e => e.NewValues)
            .HasColumnName("new_values")
            .HasColumnType("jsonb");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .HasColumnType("varchar")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(e => e.UserName)
            .HasColumnName("user_name")
            .HasColumnType("varchar")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasColumnType("varchar")
            .HasMaxLength(120);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(e => new { e.EntityName, e.EntityKey });
        builder.HasIndex(e => e.CreatedAt);
    }
}
