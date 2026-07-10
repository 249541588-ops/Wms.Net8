using Wms.Core.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ReportConfigConfiguration : IEntityTypeConfiguration<ReportConfig>
{
    public void Configure(EntityTypeBuilder<ReportConfig> builder)
    {
        builder.ToTable("ReportConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ReportCode).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.ReportCode).IsUnique();

        builder.Property(x => x.ReportName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Category).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ReportType).HasMaxLength(20);
        builder.Property(x => x.DefaultSort).HasMaxLength(200);
    }
}
