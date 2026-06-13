using global::System.ComponentModel.DataAnnotations;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities;

/// <summary>
/// 刷新 Token 实体
/// </summary>
public class RefreshToken : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// Token 值
    /// </summary>
    [Required]
    [MaxLength(500)]
    public virtual string Token { get; set; } = string.Empty;

    /// <summary>
    /// JWT Token ID（关联）
    /// </summary>
    [MaxLength(500)]
    public virtual string? JwtTokenId { get; set; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    [Required]
    public virtual int UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [Required]
    public virtual string? UserName { get; set; }

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public virtual DateTime? CreatedTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 过期时间（UTC）
    /// </summary>
    public virtual DateTime ExpiryTime { get; set; }

    /// <summary>
    /// 是否已使用
    /// </summary>
    public virtual bool IsUsed { get; set; } = false;

    /// <summary>
    /// 是否已撤销
    /// </summary>
    public virtual bool IsRevoked { get; set; } = false;

    /// <summary>
    /// 撤销时间（UTC）
    /// </summary>
    public virtual DateTime? RevokedTime { get; set; }

    /// <summary>
    /// IP 地址
    /// </summary>
    [MaxLength(50)]
    public virtual string? IpAddress { get; set; }

    /// <summary>
    /// User-Agent
    /// </summary>
    [MaxLength(500)]
    public virtual string? UserAgent { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion

    /// <summary>
    /// 检查是否过期
    /// </summary>
    public virtual bool IsExpired()
    {
        return DateTime.UtcNow >= ExpiryTime;
    }

    /// <summary>
    /// 检查是否有效
    /// </summary>
    public virtual bool IsValid()
    {
        return !IsUsed && !IsRevoked && !IsExpired();
    }
}
