using Wms.Core.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ReportExportTaskConfiguration : IEntityTypeConfiguration<ReportExportTask>
{
    public void Configure(EntityTypeBuilder<ReportExportTask> builder)
    {
        builder.ToTable("ReportExportTasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.TaskId).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.TaskId).IsUnique();

        builder.Property(x => x.ReportCode).HasMaxLength(50);
        builder.Property(x => x.UserName).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.FileName).HasMaxLength(200);
        builder.Property(x => x.FilePath).HasMaxLength(500);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.Status });
    }
}
