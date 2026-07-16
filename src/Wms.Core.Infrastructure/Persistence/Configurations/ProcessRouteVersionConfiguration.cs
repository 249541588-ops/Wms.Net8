using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Core.Domain.Entities.ProcessRoute;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ProcessRouteVersionConfiguration : IEntityTypeConfiguration<ProcessRouteVersion>
{
    public void Configure(EntityTypeBuilder<ProcessRouteVersion> builder)
    {
        builder.ToTable("ProcessRouteVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ChangeLog).HasMaxLength(500);
        builder.Property(x => x.PublishedBy).HasMaxLength(64);

        builder.HasOne(x => x.Route)
            .WithMany(r => r.Versions)
            .HasForeignKey(x => x.ProcessRouteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProcessRouteId, x.Version }).IsUnique();
    }
}
