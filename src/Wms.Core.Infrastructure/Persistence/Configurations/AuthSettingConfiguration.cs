using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

/// <summary>
/// AuthSetting 实体的 EF Core 配置
/// </summary>
internal class AuthSettingConfiguration : IEntityTypeConfiguration<AuthSetting>
{
    public void Configure(EntityTypeBuilder<AuthSetting> builder)
    {
        builder.ToTable("AuthSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.OpType);
        builder.Property(x => x.AllowedRoles).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Enabled);
    }
}