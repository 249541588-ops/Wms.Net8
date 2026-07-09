using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Repositories;

namespace Wms.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// 刷新 Token 仓储实现
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly WmsDbContext _db;
    private readonly ILogger<RefreshTokenRepository> _logger;

    /// <summary>
    /// 初始化刷新 Token 仓储类的新实例
    /// </summary>
    public RefreshTokenRepository(WmsDbContext db, ILogger<RefreshTokenRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 根据 Token 获取刷新 Token（对输入进行 SHA256 哈希后匹配）
    /// </summary>
    public RefreshToken? GetByToken(string token)
    {
        var tokenHash = ComputeSha256Hash(token);
        return _db.Set<RefreshToken>()
            .FirstOrDefault(r => r.Token == tokenHash);
    }

    /// <summary>
    /// 获取用户的所有有效 RefreshToken
    /// </summary>
    public IQueryable<RefreshToken> GetValidRefreshTokens(int userId)
    {
        var now = DateTime.UtcNow;
        return _db.Set<RefreshToken>()
            .Where(r => r.UserId == userId
                      && !r.IsUsed
                      && !r.IsRevoked
                      && r.ExpiryTime > now);
    }

    /// <summary>
    /// 创建刷新 Token
    /// </summary>
    public RefreshToken Create(RefreshToken refreshToken)
    {
        _db.Add(refreshToken);
        _db.SaveChangesAsync().Wait();
        _logger.LogInformation("创建刷新 Token: {TokenId}, 用户 ID: {UserId}", refreshToken.Id, refreshToken.UserId);
        return refreshToken;
    }

    /// <summary>
    /// 撤销所有用户的 RefreshToken
    /// </summary>
    public void RevokeAllUserTokens(int userId)
    {
        var now = DateTime.UtcNow;
        var tokens = _db.Set<RefreshToken>()
            .Where(r => r.UserId == userId && !r.IsRevoked && r.ExpiryTime > now)
            .ToList();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedTime = now;
            _db.Update(token);
        }

        _db.SaveChangesAsync().Wait();
        _logger.LogInformation("撤销用户 {UserId} 的 {Count} 个刷新 Token", userId, tokens.Count);
    }

    /// <summary>
    /// 撤销指定的 RefreshToken
    /// </summary>
    public void RevokeToken(RefreshToken refreshToken)
    {
        refreshToken.IsRevoked = true;
        refreshToken.RevokedTime = DateTime.UtcNow;
        _db.Update(refreshToken);
        _db.SaveChangesAsync().Wait();
        _logger.LogInformation("撤销刷新 Token: {TokenId}", refreshToken.Id);
    }

    /// <summary>
    /// 撤销同一 Token 家族（FamilyId）的所有 RefreshToken。
    /// 用于检测到 RefreshToken 重用时立即吊销整条刷新链路，防止被盗 token 继续生效。
    /// </summary>
    /// <param name="familyId">Token 家族 ID</param>
    /// <param name="reason">撤销原因（仅写入日志，便于审计）</param>
    /// <returns>本次实际撤销（此前未撤销）的 token 数量</returns>
    public int RevokeFamily(string familyId, string reason)
    {
        if (string.IsNullOrWhiteSpace(familyId))
        {
            _logger.LogWarning("RevokeFamily 被调用但 familyId 为空，跳过");
            return 0;
        }

        var now = DateTime.UtcNow;
        // 查询该家族所有"仍可能有效"的 token（未撤销 + 未过期）。
        // 已撤销/已过期的 token 无需重复处理；已使用（IsUsed=true）的也一并撤销以防万一。
        var familyTokens = _db.Set<RefreshToken>()
            .Where(r => r.FamilyId == familyId && !r.IsRevoked && r.ExpiryTime > now)
            .ToList();

        foreach (var token in familyTokens)
        {
            token.IsRevoked = true;
            token.IsUsed = true; // 同步标记为已使用，避免后续 GetByToken 误判
            token.RevokedTime = now;
            _db.Update(token);
        }

        if (familyTokens.Count > 0)
        {
            _db.SaveChangesAsync().Wait();
        }

        _logger.LogWarning(
            "撤销 Token 家族 {FamilyId} 的 {Count} 个 RefreshToken。原因：{Reason}",
            familyId, familyTokens.Count, reason);

        return familyTokens.Count;
    }

    /// <summary>
    /// 清理过期的 RefreshToken
    /// </summary>
    public int CleanExpiredTokens()
    {
        var now = DateTime.UtcNow;
        var expiredTokens = _db.Set<RefreshToken>()
            .Where(r => r.ExpiryTime < now || r.IsRevoked)
            .ToList();

        var count = 0;
        foreach (var token in expiredTokens)
        {
            _db.Remove(token);
            count++;
        }

        _db.SaveChangesAsync().Wait();
        _logger.LogInformation("清理 {Count} 个过期的刷新 Token", count);
        return count;
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
