using Wms.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

/// <summary>
/// RefreshToken 实体的 EF Core 配置
/// </summary>
internal class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Token);
        builder.Property(x => x.JwtTokenId);
        builder.Property(x => x.UserId);
        builder.Property(x => x.UserName);
        builder.Property(x => x.ExpiryTime);
        builder.Property(x => x.IsUsed);
        builder.Property(x => x.IsRevoked);
        builder.Property(x => x.RevokedTime);
        builder.Property(x => x.IpAddress);
        builder.Property(x => x.UserAgent);
        // R502: Token 家族 ID，用于检测 RefreshToken 重用并支持整族撤销
        builder.Property(x => x.FamilyId).HasMaxLength(64);
        // 撤销家族时按 FamilyId 高频检索，建立索引加速
        builder.HasIndex(x => x.FamilyId).HasDatabaseName("IX_RefreshTokens_FamilyId");
    }
}