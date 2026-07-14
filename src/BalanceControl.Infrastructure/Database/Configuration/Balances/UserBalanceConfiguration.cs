using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BalanceControl.Domain.Entities.Balances;

namespace BalanceControl.Infrastructure.Database.Configuration.Balances;

public sealed class UserBalanceConfiguration : IEntityTypeConfiguration<UserBalanceEntity>
{
    public void Configure(EntityTypeBuilder<UserBalanceEntity> builder)
    {
        builder.ToTable("tb_user_balance");

        builder.HasKey(balance => balance.UserId);

        builder.Property(balance => balance.UserId)
            .HasColumnName("user_id")
            .HasColumnType("varchar")
            .HasMaxLength(100);

        builder.Property(balance => balance.Balance)
            .HasColumnName("balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(balance => balance.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(balance => balance.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(balance => balance.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
