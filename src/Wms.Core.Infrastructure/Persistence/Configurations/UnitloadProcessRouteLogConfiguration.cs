using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Core.Domain.Entities.ProcessRoute;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class UnitloadProcessRouteLogConfiguration : IEntityTypeConfiguration<UnitloadProcessRouteLog>
{
    public void Configure(EntityTypeBuilder<UnitloadProcessRouteLog> builder)
    {
        builder.ToTable("UnitloadProcessRouteLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.OperationCode).HasMaxLength(50);
        builder.Property(x => x.ActionType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.FromOperation).HasMaxLength(50);
        builder.Property(x => x.ToOperation).HasMaxLength(50);
        builder.Property(x => x.Operator).HasMaxLength(64);
        builder.Property(x => x.Remark).HasMaxLength(500);

        builder.HasIndex(x => x.UnitloadId);
        builder.HasIndex(x => x.VersionId);
    }
}
