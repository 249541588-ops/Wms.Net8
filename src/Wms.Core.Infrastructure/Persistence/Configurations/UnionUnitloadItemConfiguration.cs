using Wms.Core.Domain.Entities.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class UnionUnitloadItemConfiguration : IEntityTypeConfiguration<UnionUnitloadItem>
{
    public void Configure(EntityTypeBuilder<UnionUnitloadItem> builder)
    {
        builder.ToTable("UnionUnitloadItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
