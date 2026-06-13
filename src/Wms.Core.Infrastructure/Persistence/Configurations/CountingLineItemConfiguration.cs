using Wms.Core.Domain.Entities.Counting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class CountingLineItemConfiguration : IEntityTypeConfiguration<CountingLineItem>
{
    public void Configure(EntityTypeBuilder<CountingLineItem> builder)
    {
        builder.ToTable("CountingLineItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
