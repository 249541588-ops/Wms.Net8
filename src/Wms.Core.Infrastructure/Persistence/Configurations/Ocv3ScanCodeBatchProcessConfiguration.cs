using Wms.Core.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class Ocv3ScanCodeBatchProcessConfiguration : IEntityTypeConfiguration<Ocv3ScanCodeBatchProcess>
{
    public void Configure(EntityTypeBuilder<Ocv3ScanCodeBatchProcess> builder)
    {
        builder.ToTable("Ocv3ScanCodeBatchProcess");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
    }
}
