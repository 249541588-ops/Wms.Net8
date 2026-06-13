using Wms.Core.Domain.Entities.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ArchivedUnitloadItemConfiguration : IEntityTypeConfiguration<ArchivedUnitloadItem>
{
    public void Configure(EntityTypeBuilder<ArchivedUnitloadItem> builder)
    {
        builder.ToTable("ArchivedUnitloadItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
