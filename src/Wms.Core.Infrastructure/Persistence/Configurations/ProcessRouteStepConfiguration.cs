using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Core.Domain.Entities.ProcessRoute;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ProcessRouteStepConfiguration : IEntityTypeConfiguration<ProcessRouteStep>
{
    public void Configure(EntityTypeBuilder<ProcessRouteStep> builder)
    {
        builder.ToTable("ProcessRouteSteps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.OperationCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.StepType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasOne(x => x.Version)
            .WithMany(v => v.Steps)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.VersionId);
    }
}
