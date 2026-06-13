using Wms.Core.Domain.Entities.Material;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

internal class MaterialConfiguration : IEntityTypeConfiguration<Materials>
{
    public void Configure(EntityTypeBuilder<Materials> builder)
    {
        builder.ToTable("Materials");
        builder.HasKey(x => x.MaterialId);
    }
}
