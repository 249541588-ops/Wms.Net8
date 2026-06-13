using Wms.Core.Domain.Entities.Counting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class CountingOrderConfiguration : IEntityTypeConfiguration<CountingOrder>
{
    public void Configure(EntityTypeBuilder<CountingOrder> builder)
    {
        builder.ToTable("CountingOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
