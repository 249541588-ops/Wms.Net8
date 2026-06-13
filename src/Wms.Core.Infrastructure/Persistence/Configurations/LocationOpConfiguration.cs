using Wms.Core.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class LocationOpConfiguration : IEntityTypeConfiguration<LocationOp>
{
    public void Configure(EntityTypeBuilder<LocationOp> builder)
    {
        builder.ToTable("LocationOps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
