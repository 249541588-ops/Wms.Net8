using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Core.Domain.Entities.ProcessRoute;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class ProcessRouteMaterialBindingConfiguration : IEntityTypeConfiguration<ProcessRouteMaterialBinding>
{
    public void Configure(EntityTypeBuilder<ProcessRouteMaterialBinding> builder)
    {
        builder.ToTable("ProcessRouteMaterialBindings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.HasOne(x => x.Route)
            .WithMany(r => r.MaterialBindings)
            .HasForeignKey(x => x.ProcessRouteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Material)
            .WithMany()
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProcessRouteId, x.MaterialId }).IsUnique();
    }
}
