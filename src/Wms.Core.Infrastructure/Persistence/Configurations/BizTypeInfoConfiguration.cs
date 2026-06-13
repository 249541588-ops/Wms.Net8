using Wms.Core.Domain.Entities.Inbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class BizTypeInfoConfiguration : IEntityTypeConfiguration<BizTypeInfo>
{
    public void Configure(EntityTypeBuilder<BizTypeInfo> builder)
    {
        builder.ToTable("BizTypeInfos");
        builder.HasKey(x => x.BizTypeCode);
    }
}
