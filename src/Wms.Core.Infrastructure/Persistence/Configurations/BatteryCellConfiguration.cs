using Wms.Core.Domain.Entities.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class BatteryCellConfiguration : IEntityTypeConfiguration<BatteryCell>
{
    public void Configure(EntityTypeBuilder<BatteryCell> builder)
    {
        builder.ToTable("BatteryCells");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        // 条码必填
        builder.Property(x => x.BarCode).IsRequired();

        // 物料外键
        builder.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId);
    }
}
