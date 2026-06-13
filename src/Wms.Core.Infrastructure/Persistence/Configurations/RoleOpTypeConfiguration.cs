using Wms.Core.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class RoleOpTypeConfiguration : IEntityTypeConfiguration<RoleOpType>
{
    public void Configure(EntityTypeBuilder<RoleOpType> builder)
    {
        builder.ToTable("ROLE_OPTYPE");
        builder.HasKey(x => new { x.RoleId, x.OpType });
    }
}
