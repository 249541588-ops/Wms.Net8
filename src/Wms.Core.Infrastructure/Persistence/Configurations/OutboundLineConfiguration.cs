using Wms.Core.Domain.Entities.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class OutboundLineConfiguration : IEntityTypeConfiguration<OutboundLine>
{
    public void Configure(EntityTypeBuilder<OutboundLine> builder)
    {
        builder.ToTable("OutboundLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
