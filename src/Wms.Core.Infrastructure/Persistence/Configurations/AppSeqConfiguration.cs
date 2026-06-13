using Wms.Core.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class AppSeqConfiguration : IEntityTypeConfiguration<AppSeq>
{
    public void Configure(EntityTypeBuilder<AppSeq> builder)
    {
        builder.ToTable("AppSeqs");
        builder.HasKey(x => x.SeqName);
    }
}
