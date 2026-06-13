using Wms.Core.Domain.Entities.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class LanewayPortConfiguration : IEntityTypeConfiguration<LanewayPort>
{
    public void Configure(EntityTypeBuilder<LanewayPort> builder)
    {
        builder.ToTable("Laneway_Port");
        builder.HasKey(x => new { x.LanewayId, x.PortId });

        builder.HasOne(x => x.Port).WithMany().HasForeignKey(x => x.PortId);
        builder.HasOne(x => x.Laneway).WithMany().HasForeignKey(x => x.LanewayId);
    }
}
