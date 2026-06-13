using Wms.Core.Domain.Entities;

namespace Wms.Core.Domain.Repositories;

/// <summary>
/// 刷新 Token 仓储接口
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// 根据 Token 获取刷新 Token
    /// </summary>
    RefreshToken? GetByToken(string token);

    /// <summary>
    /// 获取用户的所有有效 RefreshToken
    /// </summary>
    IQueryable<RefreshToken> GetValidRefreshTokens(int userId);

    /// <summary>
    /// 创建刷新 Token
    /// </summary>
    RefreshToken Create(RefreshToken refreshToken);

    /// <summary>
    /// 撤销所有用户的 RefreshToken
    /// </summary>
    void RevokeAllUserTokens(int userId);

    /// <summary>
    /// 撤销指定的 RefreshToken
    /// </summary>
    void RevokeToken(RefreshToken refreshToken);

    /// <summary>
    /// 清理过期的 RefreshToken
    /// </summary>
    int CleanExpiredTokens();
}
