using Wms.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

/// <summary>
/// Sys_Language 实体的 EF Core 配置
/// </summary>
internal class Sys_LanguageConfiguration : IEntityTypeConfiguration<Sys_Language>
{
    public void Configure(EntityTypeBuilder<Sys_Language> builder)
    {
        builder.ToTable("Sys_Language");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Chinese).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ChineseDesc);
        builder.Property(x => x.English);
        builder.Property(x => x.Deutsch);
        builder.Property(x => x.Indonesian);
        builder.Property(x => x.Module);
        builder.Property(x => x.IsPackageContent);
        builder.Property(x => x.Creator);
        builder.Property(x => x.CreateDate);
        builder.Property(x => x.ModifyDate);
        builder.Property(x => x.Modifier);
    }
}