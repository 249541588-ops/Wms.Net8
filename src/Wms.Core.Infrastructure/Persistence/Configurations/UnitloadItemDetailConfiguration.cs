using Wms.Core.Domain.Entities.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class UnitloadItemDetailConfiguration : IEntityTypeConfiguration<UnitloadItemDetail>
{
    public void Configure(EntityTypeBuilder<UnitloadItemDetail> builder)
    {
        builder.ToTable("UnitloadItemDetails");
        builder.HasKey(x => x.UnitloadItemDetailId);
        builder.Property(x => x.UnitloadItemDetailId).ValueGeneratedOnAdd();

        builder.HasOne(x => x.UnitloadItem).WithMany(x => x.UnitloadItemDetails).HasForeignKey(x => x.UnitloadItemId);
    }
}
