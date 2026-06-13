using Wms.Core.Domain.Entities.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ArchivedTaskConfiguration : IEntityTypeConfiguration<ArchivedTask>
{
    public void Configure(EntityTypeBuilder<ArchivedTask> builder)
    {
        builder.ToTable("ArchivedTasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
