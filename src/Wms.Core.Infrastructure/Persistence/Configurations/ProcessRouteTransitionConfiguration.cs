using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Core.Domain.Entities.ProcessRoute;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ProcessRouteTransitionConfiguration : IEntityTypeConfiguration<ProcessRouteTransition>
{
    public void Configure(EntityTypeBuilder<ProcessRouteTransition> builder)
    {
        builder.ToTable("ProcessRouteTransitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.TransitionType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Label).HasMaxLength(100);

        builder.HasOne(x => x.Version)
            .WithMany(v => v.Transitions)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FromStep)
            .WithMany()
            .HasForeignKey(x => x.FromStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToStep)
            .WithMany()
            .HasForeignKey(x => x.ToStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.VersionId, x.FromStepId, x.ToStepId });
    }
}
