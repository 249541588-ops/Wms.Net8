using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
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
    /// 根据 Token 获取刷新 Token
    /// </summary>
    public RefreshToken? GetByToken(string token)
    {
        return _db.Set<RefreshToken>()
            .FirstOrDefault(r => r.Token == token);
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
}
