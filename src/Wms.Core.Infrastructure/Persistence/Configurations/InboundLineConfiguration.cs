using Wms.Core.Domain.Entities.Inbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class InboundLineConfiguration : IEntityTypeConfiguration<InboundLine>
{
    public void Configure(EntityTypeBuilder<InboundLine> builder)
    {
        builder.ToTable("InboundLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
