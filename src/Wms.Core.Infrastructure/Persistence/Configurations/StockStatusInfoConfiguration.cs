using Wms.Core.Domain.Entities.Material;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class StockStatusInfoConfiguration : IEntityTypeConfiguration<StockStatusInfo>
{
    public void Configure(EntityTypeBuilder<StockStatusInfo> builder)
    {
        builder.ToTable("StockStatusInfos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
