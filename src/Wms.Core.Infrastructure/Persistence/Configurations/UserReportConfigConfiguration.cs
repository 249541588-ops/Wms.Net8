using Wms.Core.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class UserReportConfigConfiguration : IEntityTypeConfiguration<UserReportConfig>
{
    public void Configure(EntityTypeBuilder<UserReportConfig> builder)
    {
        builder.ToTable("UserReportConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.UserName).HasMaxLength(100);
        builder.Property(x => x.ReportCode).HasMaxLength(50);
        builder.Property(x => x.ConfigName).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => new { x.UserId, x.ReportCode });
    }
}
