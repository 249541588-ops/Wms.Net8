using Wms.Core.Domain.Entities.Counting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class CountingLineItemDetailConfiguration : IEntityTypeConfiguration<CountingLineItemDetail>
{
    public void Configure(EntityTypeBuilder<CountingLineItemDetail> builder)
    {
        builder.ToTable("CountingLineItemDetails");
        builder.HasKey(x => x.CountingLineItemDetailId);
    }
}
