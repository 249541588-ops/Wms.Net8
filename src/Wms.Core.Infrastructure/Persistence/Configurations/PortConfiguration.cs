using Wms.Core.Domain.Entities.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class PortConfiguration : IEntityTypeConfiguration<Port>
{
    public void Configure(EntityTypeBuilder<Port> builder)
    {
        builder.ToTable("Ports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.PortCode).HasMaxLength(255);
        builder.Property(x => x.PortName).HasMaxLength(255);
        builder.Property(x => x.PortType).HasMaxLength(255);
        builder.Property(x => x.Comment).HasMaxLength(255);
        builder.Property(x => x.CurrentUatType).HasMaxLength(30);
        builder.Property(x => x.CreatedBy).HasMaxLength(20);
        builder.Property(x => x.ModifiedBy).HasMaxLength(20);
    }
}
