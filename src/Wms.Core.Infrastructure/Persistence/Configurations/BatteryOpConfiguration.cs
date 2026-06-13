using Wms.Core.Domain.Entities.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class BatteryOpConfiguration : IEntityTypeConfiguration<BatteryOp>
{
    public void Configure(EntityTypeBuilder<BatteryOp> builder)
    {
        builder.ToTable("BatteryOps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
