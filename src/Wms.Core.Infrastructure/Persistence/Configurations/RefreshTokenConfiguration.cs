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
    }
}