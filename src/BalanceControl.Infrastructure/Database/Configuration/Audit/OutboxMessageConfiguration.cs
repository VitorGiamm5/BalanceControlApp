using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BalanceControl.Domain.Entities.Audit;

namespace BalanceControl.Infrastructure.Database.Configuration.Audit;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        builder.ToTable("tb_outbox_message");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasColumnType("varchar")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasColumnType("varchar")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Attempts)
            .HasColumnName("attempts")
            .IsRequired();

        builder.Property(e => e.Error)
            .HasColumnName("error")
            .HasColumnType("text");

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at");

        builder.HasIndex(e => new { e.Status, e.OccurredAt });
    }
}
