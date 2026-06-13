using Wms.Core.Domain.Entities.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class UnitloadConfiguration : IEntityTypeConfiguration<Unitload>
{
    public void Configure(EntityTypeBuilder<Unitload> builder)
    {
        builder.ToTable("Unitloads");
        builder.HasKey(x => x.UnitloadId);
        builder.Property(x => x.UnitloadId).ValueGeneratedOnAdd();

        builder.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId);
    }
}
