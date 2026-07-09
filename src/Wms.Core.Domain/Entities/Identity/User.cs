using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using global::System.Text.Json.Serialization;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Identity;

/// <summary>
/// 用户 - 表示系统用户
/// </summary>
public class User : IEntity<int>, IAuditable
{

    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    #region IAuditable 实现

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    [MaxLength(64)]
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(64)]
    public virtual string? ModifiedBy { get; set; }

    #endregion

    /// <summary>
    /// 用户名（自然键）
    /// </summary>
    [Required]
    [MaxLength(64)]
    public virtual string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希值
    /// </summary>
    [Required]
    [MaxLength(256)]
    [JsonIgnore]
    public virtual string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 密码盐值
    /// </summary>
    [Required]
    [MaxLength(128)]
    [JsonIgnore]
    public virtual string PasswordSalt { get; set; } = string.Empty;

    /// <summary>
    /// 真实姓名
    /// </summary>
    [MaxLength(64)]
    public virtual string? RealName { get; set; }

    /// <summary>
    /// 电子邮件
    /// </summary>
    [MaxLength(128)]
    public virtual string? Email { get; set; }

    /// <summary>
    /// 手机号码
    /// </summary>
    [MaxLength(20)]
    public virtual string? PhoneNumber { get; set; }

    /// <summary>
    /// 是否内置用户
    /// </summary>
    public virtual bool IsBuiltIn { get; set; }

    /// <summary>
    /// 是否锁定
    /// </summary>
    public virtual bool IsLocked { get; set; }

    /// <summary>
    /// 锁定原因
    /// </summary>
    [MaxLength(255)]
    public virtual string? LockedReason { get; set; }

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public virtual DateTime? LastLoginTime { get; set; }

    /// <summary>
    /// 最后登录IP
    /// </summary>
    [MaxLength(45)]
    public virtual string? LastLoginIp { get; set; }

    /// <summary>
    /// 是否激活
    /// </summary>
    public virtual bool IsActive { get; set; } = true;

    /// <summary>
    /// 软删除时间（UTC）。null 表示未删除；非 null 表示已被软删除。
    /// 软删除时同时将 <see cref="IsActive"/> 置为 false，保留此字段用于区分"软删除"与"普通禁用"。
    /// </summary>
    public virtual DateTime? DeletedAt { get; set; }

    /// <summary>
    /// 软删除操作人
    /// </summary>
    [MaxLength(64)]
    public virtual string? DeletedBy { get; set; }

    /// <summary>
    /// 角色名称（不映射到数据库，仅用于 API 返回）
    /// </summary>
    [global::System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [JsonPropertyName("role")]
    public virtual string? Role { get; set; }

    /// <summary>
    /// 用户角色集合
    /// </summary>
    [JsonIgnore]
    public virtual ISet<Role> Roles { get; set; } = new HashSet<Role>();

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion

    /// <summary>
    /// 初始化用户类的新实例
    /// </summary>
    public User()
    {
        CreatedTime = DateTime.UtcNow;
        ModifiedTime = DateTime.UtcNow;
        IsActive = true;
    }

    /// <summary>
    /// 添加角色
    /// </summary>
    public virtual void AddRole(Role role)
    {
        Roles.Add(role);
        ModifiedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 移除角色
    /// </summary>
    public virtual void RemoveRole(Role role)
    {
        Roles.Remove(role);
        ModifiedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 检查用户是否有指定权限
    /// 注意：此方法已废弃，权限通过 AuthSetting 实体管理
    /// </summary>
    /// <param name="permission">权限代码</param>
    [Obsolete("权限检查请使用 AuthSetting 实体")]
    public virtual bool HasPermission(string permission)
    {
        return false; // 权限已通过 AuthSetting 管理，此方法返回 false
    }

    /// <summary>
    /// 锁定用户
    /// </summary>
    /// <param name="reason">锁定原因</param>
    public virtual void Lock(string reason)
    {
        IsLocked = true;
        LockedReason = reason;
        ModifiedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 解锁用户
    /// </summary>
    public virtual void Unlock()
    {
        IsLocked = false;
        LockedReason = null;
        ModifiedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 验证密码（兼容 BCrypt 和旧 HMAC 格式）
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>是否匹配</returns>
    public virtual bool ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        // BCrypt hash 以 "$2" 开头
        if (PasswordHash != null && PasswordHash.StartsWith("$2"))
        {
            return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
        }

        // 旧 HMAC 格式兼容
        var hash = HashPassword(password, PasswordSalt);
        return hash == PasswordHash;
    }

    /// <summary>
    /// 设置密码（使用 BCrypt）
    /// </summary>
    /// <param name="password">明文密码</param>
    public virtual void SetPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("密码不能为空", nameof(password));
        }

        PasswordSalt = string.Empty; // BCrypt 自带 salt，不再需要单独存储
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        ModifiedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 检查密码是否为 BCrypt 格式
    /// </summary>
    public virtual bool IsBcryptHash()
    {
        return PasswordHash != null && PasswordHash.StartsWith("$2");
    }

    /// <summary>
    /// 检查密码是否需要升级为 BCrypt（旧 HMAC 格式）
    /// </summary>
    public virtual bool NeedsPasswordRehash()
    {
        return PasswordHash != null && !PasswordHash.StartsWith("$2");
    }

    /// <summary>
    /// 记录登录
    /// </summary>
    /// <param name="ipAddress">IP地址</param>
    public virtual void RecordLogin(string? ipAddress = null)
    {
        LastLoginTime = DateTime.UtcNow;
        LastLoginIp = ipAddress;
        ModifiedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 生成密码盐值
    /// </summary>
    private static string GenerateSalt()
    {
        using var rng = global::System.Security.Cryptography.RandomNumberGenerator.Create();
        var randomBytes = new byte[16];
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// 哈希密码
    /// </summary>
    private static string HashPassword(string password, string salt)
    {
        using var hmac = new global::System.Security.Cryptography.HMACSHA256(
            global::System.Text.Encoding.UTF8.GetBytes(salt));

        var hash = hmac.ComputeHash(global::System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hash);
    }
}

/// <summary>
/// 
/// </summary>
public class UserRoles : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public virtual int RoleId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public virtual int UserId { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion

}