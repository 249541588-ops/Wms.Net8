using global::System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
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

    /// <summary>
    /// Token 家族 ID（用于检测 RefreshToken 重用，对应 RFC 6749 Section 10.4）
    /// </summary>
    /// <remarks>
    /// 同一次登录产生的所有 RefreshToken（包括后续刷新链路产生的）共享同一个 FamilyId。
    /// 检测到已被使用（IsUsed=true）或已撤销（IsRevoked=true）的 token 再次被提交刷新，
    /// 视为 token 被盗用，立即吊销整个 FamilyId 家族的所有 token。
    /// 登录时生成新 FamilyId；刷新时新 token 继承旧 token 的 FamilyId。
    /// 存储格式：Guid.ToString("N")（32 位十六进制无连字符）。
    /// </remarks>
    [MaxLength(64)]
    public virtual string? FamilyId { get; set; }

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

    /// <summary>
    /// 对原始 Token 进行 SHA256 哈希后存储
    /// </summary>
    /// <param name="token">原始明文 Token</param>
    public void SetTokenHash(string token)
    {
        Token = ComputeSha256Hash(token);
    }

    /// <summary>
    /// 验证输入的 Token 是否与存储的哈希匹配
    /// </summary>
    /// <param name="token">待验证的明文 Token</param>
    /// <returns>是否匹配</returns>
    public bool VerifyToken(string token)
    {
        return Token == ComputeSha256Hash(token);
    }

    /// <summary>
    /// 计算 SHA256 哈希值
    /// </summary>
    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
