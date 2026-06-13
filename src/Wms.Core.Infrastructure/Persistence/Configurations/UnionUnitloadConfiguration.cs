using Wms.Core.Domain.Entities.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class UnionUnitloadConfiguration : IEntityTypeConfiguration<UnionUnitload>
{
    public void Configure(EntityTypeBuilder<UnionUnitload> builder)
    {
        builder.ToTable("UnionUnitloads");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
