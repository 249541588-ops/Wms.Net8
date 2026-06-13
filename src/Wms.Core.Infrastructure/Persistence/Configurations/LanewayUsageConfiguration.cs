using Wms.Core.Domain.Entities.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class LanewayUsageConfiguration : IEntityTypeConfiguration<LanewayUsage>
{
    public void Configure(EntityTypeBuilder<LanewayUsage> builder)
    {
        builder.ToTable("LanewayUsage");
        builder.HasKey(x => new { x.LanewayId, x.StorageGroup, x.Specification, x.WeightLimit, x.HeightLimit });
    }
}
