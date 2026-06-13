using Wms.Core.Domain.Entities.Counting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class CountingLineConfiguration : IEntityTypeConfiguration<CountingLine>
{
    public void Configure(EntityTypeBuilder<CountingLine> builder)
    {
        builder.ToTable("CountingLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
