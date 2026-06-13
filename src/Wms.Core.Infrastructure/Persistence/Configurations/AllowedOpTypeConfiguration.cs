using Wms.Core.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class AllowedOpTypeConfiguration : IEntityTypeConfiguration<AllowedOpType>
{
    public void Configure(EntityTypeBuilder<AllowedOpType> builder)
    {
        builder.ToTable("AllowedOpTypes");
        builder.HasKey(x => new { x.role_key, x.id });
    }
}
