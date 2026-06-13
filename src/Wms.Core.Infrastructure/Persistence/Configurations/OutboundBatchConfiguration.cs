using Wms.Core.Domain.Entities.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class OutboundBatchConfiguration : IEntityTypeConfiguration<OutboundBatch>
{
    public void Configure(EntityTypeBuilder<OutboundBatch> builder)
    {
        builder.ToTable("OutboundBatch");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
