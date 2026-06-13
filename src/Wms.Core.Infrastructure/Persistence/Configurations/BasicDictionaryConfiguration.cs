using Wms.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

/// <summary>
/// BasicDictionary 实体的 EF Core 配置
/// </summary>
internal class BasicDictionaryConfiguration : IEntityTypeConfiguration<BasicDictionary>
{
    public void Configure(EntityTypeBuilder<BasicDictionary> builder)
    {
        builder.ToTable("BasicDictionary");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.No);
        builder.Property(x => x.ParentId);
        builder.Property(x => x.ModifiedTime);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.Name);
        builder.Property(x => x.Value);
        builder.Property(x => x.Abbreviation);
        builder.Property(x => x.FullPinyin);
        builder.Property(x => x.Remarks);
        builder.Property(x => x.Sort);
        builder.Property(x => x.Status);
        builder.Property(x => x.IsNext).HasConversion(
            v => v != 0,
            v => v ? 1 : 0);
        builder.Property(x => x.ExpandField1);
        builder.Property(x => x.ExpandField2);
    }
}