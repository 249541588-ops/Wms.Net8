using Wms.Core.Domain.Entities.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class TransTaskConfiguration : IEntityTypeConfiguration<TransTask>
{
    public void Configure(EntityTypeBuilder<TransTask> builder)
    {
        builder.ToTable("TransTasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.StartLocationId).IsRequired();
        builder.Property(x => x.EndLocationId).IsRequired();

        builder.HasOne(x => x.Unitload)
            .WithMany()
            .HasForeignKey(x => x.UnitloadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StartLocation)
            .WithMany()
            .HasForeignKey(x => x.StartLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EndLocation)
            .WithMany()
            .HasForeignKey(x => x.EndLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
