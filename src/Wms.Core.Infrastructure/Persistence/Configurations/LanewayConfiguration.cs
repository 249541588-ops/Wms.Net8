using Wms.Core.Domain.Entities.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class LanewayConfiguration : IEntityTypeConfiguration<Laneway>
{
    public void Configure(EntityTypeBuilder<Laneway> builder)
    {
        builder.ToTable("Laneways");
        builder.HasKey(x => x.LanewayId);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
    }
}
