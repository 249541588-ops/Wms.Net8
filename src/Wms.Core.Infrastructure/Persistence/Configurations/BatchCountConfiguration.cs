using Wms.Core.Domain.Entities.StockFlow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class BatchCountConfiguration : IEntityTypeConfiguration<BatchCount>
{
    public void Configure(EntityTypeBuilder<BatchCount> builder)
    {
        builder.ToTable("BatchCount");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
