using Wms.Core.Domain.Entities.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class UnitloadItemConfiguration : IEntityTypeConfiguration<UnitloadItem>
{
    public void Configure(EntityTypeBuilder<UnitloadItem> builder)
    {
        builder.ToTable("UnitloadItems");
        builder.HasKey(x => x.UnitloadItemId);
        builder.Property(x => x.UnitloadItemId).ValueGeneratedOnAdd();

        builder.HasOne(x => x.Unitload).WithMany(x => x.UnitloadItems).HasForeignKey(x => x.UnitloadId);
        builder.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId);
    }
}
