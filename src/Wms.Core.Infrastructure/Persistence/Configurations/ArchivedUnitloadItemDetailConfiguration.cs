using Wms.Core.Domain.Entities.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ArchivedUnitloadItemDetailConfiguration : IEntityTypeConfiguration<ArchivedUnitloadItemDetail>
{
    public void Configure(EntityTypeBuilder<ArchivedUnitloadItemDetail> builder)
    {
        builder.ToTable("ArchivedUnitloadItemDetails");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
