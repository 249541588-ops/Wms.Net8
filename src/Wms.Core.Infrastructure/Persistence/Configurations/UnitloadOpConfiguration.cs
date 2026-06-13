using Wms.Core.Domain.Entities.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class UnitloadOpConfiguration : IEntityTypeConfiguration<UnitloadOp>
{
    public void Configure(EntityTypeBuilder<UnitloadOp> builder)
    {
        builder.ToTable("UnitloadOps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
