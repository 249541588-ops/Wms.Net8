using Wms.Core.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class SysTimedTaskConfiguration : IEntityTypeConfiguration<SysTimedTask>
{
    public void Configure(EntityTypeBuilder<SysTimedTask> builder)
    {
        builder.ToTable("TSysTimedTask");
        builder.HasKey(x => new { x.TaskName, x.GroupName });
    }
}
