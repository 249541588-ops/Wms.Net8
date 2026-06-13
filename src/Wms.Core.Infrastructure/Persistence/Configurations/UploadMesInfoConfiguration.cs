using Wms.Core.Domain.Entities.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class UploadMesInfoConfiguration : IEntityTypeConfiguration<UploadMesInfo>
{
    public void Configure(EntityTypeBuilder<UploadMesInfo> builder)
    {
        builder.ToTable("UploadMesInfo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
