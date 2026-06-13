using Wms.Core.Domain.Entities.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class OutboundLineAllocationConfiguration : IEntityTypeConfiguration<OutboundLineAllocation>
{
    public void Configure(EntityTypeBuilder<OutboundLineAllocation> builder)
    {
        builder.ToTable("OutboundLineAllocations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
