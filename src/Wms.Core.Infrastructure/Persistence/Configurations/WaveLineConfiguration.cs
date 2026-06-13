using Wms.Core.Domain.Entities.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class WaveLineConfiguration : IEntityTypeConfiguration<WaveLine>
{
    public void Configure(EntityTypeBuilder<WaveLine> builder)
    {
        builder.ToTable("WaveLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
