using Wms.Core.Domain.Entities.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ArchivedUnitloadConfiguration : IEntityTypeConfiguration<ArchivedUnitload>
{
    public void Configure(EntityTypeBuilder<ArchivedUnitload> builder)
    {
        builder.ToTable("ArchivedUnitloads");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
