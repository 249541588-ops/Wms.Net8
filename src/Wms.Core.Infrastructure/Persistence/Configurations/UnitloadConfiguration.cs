using Wms.Core.Domain.Entities.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class UnitloadConfiguration : IEntityTypeConfiguration<Unitload>
{
    public void Configure(EntityTypeBuilder<Unitload> builder)
    {
        builder.ToTable("Unitloads");
        builder.HasKey(x => x.UnitloadId);
        builder.Property(x => x.UnitloadId).ValueGeneratedOnAdd();

        builder.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId);

        // 工艺路线相关字段
        builder.Property(x => x.ProcessRouteId).HasColumnName("ProcessRouteId");
        builder.Property(x => x.ProcessRouteVersionId).HasColumnName("ProcessRouteVersionId");
        builder.Property(x => x.CurrentStepId).HasColumnName("CurrentStepId");
        builder.Property(x => x.NextStepId).HasColumnName("NextStepId");
        builder.Property(x => x.IsAwaitingBranchSelection).HasColumnName("IsAwaitingBranchSelection").HasDefaultValue(false);
    }
}
