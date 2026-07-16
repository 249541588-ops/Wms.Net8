using Wms.Core.Domain.Entities.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ArchivedUnitloadConfiguration : IEntityTypeConfiguration<ArchivedUnitload>
{
    public void Configure(EntityTypeBuilder<ArchivedUnitload> builder)
    {
        builder.ToTable("ArchivedUnitloads");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        // 工艺路线相关字段
        builder.Property(x => x.ProcessRouteId).HasColumnName("ProcessRouteId");
        builder.Property(x => x.ProcessRouteVersionId).HasColumnName("ProcessRouteVersionId");
        builder.Property(x => x.CurrentStepId).HasColumnName("CurrentStepId");
        builder.Property(x => x.NextStepId).HasColumnName("NextStepId");
        builder.Property(x => x.IsAwaitingBranchSelection).HasColumnName("IsAwaitingBranchSelection").HasDefaultValue(false);
    }
}
