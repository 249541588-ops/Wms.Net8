using Wms.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

/// <summary>
/// Menus 实体的 EF Core 配置
/// </summary>
internal class MenusConfiguration : IEntityTypeConfiguration<Menus>
{
    public void Configure(EntityTypeBuilder<Menus> builder)
    {
        builder.ToTable("Menus");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ParentId);
        builder.Property(x => x.Sort);
        builder.Property(x => x.Name).HasMaxLength(50);
        builder.Property(x => x.EnglishName).HasMaxLength(50);
        builder.Property(x => x.GermanName).HasMaxLength(50);
        builder.Property(x => x.Url).HasMaxLength(150);
        builder.Property(x => x.ImgUrl);
        builder.Property(x => x.IsDisplay);
        builder.Property(x => x.FunctionButton);
        builder.Property(x => x.Creator);
        builder.Property(x => x.Editor);
        builder.Property(x => x.CreateTime);
        builder.Property(x => x.EditTime);
    }
}
