using Wms.Core.Domain.Entities.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class BatteryCellSortingConfiguration : IEntityTypeConfiguration<BatteryCellSorting>
{
    public void Configure(EntityTypeBuilder<BatteryCellSorting> builder)
    {
        builder.ToTable("BatteryCellSorting");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        // 物料外键
        builder.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId);
    }
}
