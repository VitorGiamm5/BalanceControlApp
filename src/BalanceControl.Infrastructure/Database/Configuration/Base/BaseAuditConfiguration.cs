using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BalanceControl.Domain.Entities.Base;

namespace BalanceControl.Infrastructure.Database.Configuration.Base;

public abstract class BaseAuditConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : BaseAuditEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.Property(e => e.CreatedDate)
               .HasColumnName("created_date")
               .HasColumnType("timestamp");

        builder.Property(e => e.UpdatedDate)
               .HasColumnName("updated_date")
               .HasColumnType("timestamp");

        builder.Property(e => e.DeletedDate)
               .HasColumnName("deleted_date")
               .HasColumnType("timestamp");

        builder.Property(e => e.CreatedByUserId)
               .HasColumnName("created_by_user_id");

        builder.Property(e => e.UpdatedByUserId)
               .HasColumnName("updated_by_user_id");

        builder.Property(e => e.DeletedByUserId)
               .HasColumnName("deleted_by_user_id");

        builder.Property(e => e.IsDeleted)
               .HasColumnName("is_deleted")
               .HasDefaultValue(false);

        builder.Property(e => e.IsActive)
               .HasColumnName("is_active")
               .HasDefaultValue(true);

        // Filtro global — exclui automaticamente registros deletados de todas as queries
        builder.HasQueryFilter(e => e.IsDeleted != true);
    }
}
