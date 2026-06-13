using Wms.Core.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class LocationAllocRuleStatConfiguration : IEntityTypeConfiguration<LocationAllocRuleStat>
{
    public void Configure(EntityTypeBuilder<LocationAllocRuleStat> builder)
    {
        builder.ToTable("LocationAllocRuleStats");
        builder.HasKey(x => x.RuleName);
    }
}
