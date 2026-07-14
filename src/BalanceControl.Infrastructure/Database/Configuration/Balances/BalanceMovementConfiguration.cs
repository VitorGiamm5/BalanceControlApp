using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BalanceControl.Domain.Entities.Balances;

namespace BalanceControl.Infrastructure.Database.Configuration.Balances;

public sealed class BalanceMovementConfiguration : IEntityTypeConfiguration<BalanceMovementEntity>
{
    public void Configure(EntityTypeBuilder<BalanceMovementEntity> builder)
    {
        builder.ToTable("tb_balance_movement");

        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Id)
            .HasColumnName("id");

        builder.Property(movement => movement.UserId)
            .HasColumnName("user_id")
            .HasColumnType("varchar")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(movement => movement.OperationId)
            .HasColumnName("operation_id")
            .IsRequired();

        builder.Property(movement => movement.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(movement => movement.BalanceAfter)
            .HasColumnName("balance_after")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(movement => movement.RequestHash)
            .HasColumnName("request_hash")
            .HasColumnType("char")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(movement => movement.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(movement => movement.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(movement => movement.Description)
            .HasColumnName("description")
            .HasColumnType("varchar")
            .HasMaxLength(500);

        builder.HasIndex(movement => new { movement.UserId, movement.OperationId })
            .IsUnique();

        builder.HasIndex(movement => new { movement.UserId, movement.CreatedAt, movement.Id });
    }
}
