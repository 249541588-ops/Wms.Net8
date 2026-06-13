using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wms.Core.Infrastructure.Persistence.Configurations;

/// <summary>
/// User 实体的 EF Core 配置
/// </summary>
internal class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.UserName).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PasswordSalt).HasMaxLength(128).IsRequired();
        builder.Property(x => x.IsBuiltIn);
        builder.Property(x => x.IsLocked);
        builder.Property(x => x.LastLoginTime);
        builder.Property(x => x.IsActive);
        builder.Property(x => x.ModifiedTime);

        // User-Roles many-to-many via UserRoles join table
        builder.HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<UserRoles>(
                j => j.HasOne<Role>().WithMany().HasForeignKey(ur => ur.RoleId),
                j => j.HasOne<User>().WithMany().HasForeignKey(ur => ur.UserId),
                j =>
                {
                    j.ToTable("UserRoles");
                    j.HasKey(ur => ur.Id);
                }
            );
    }
}