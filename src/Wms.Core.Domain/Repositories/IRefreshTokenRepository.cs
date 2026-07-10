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
    /// 撤销同一 Token 家族（FamilyId）的所有 RefreshToken
    /// </summary>
    /// <remarks>
    /// 用于 RFC 6749 Section 10.4 规定的 RefreshToken 重用检测：
    /// 一旦发现已被使用或已撤销的 token 再次被提交刷新，立即吊销整个家族，
    /// 防止攻击者利用被盗的 token 链路继续刷新。
    /// </remarks>
    /// <param name="familyId">Token 家族 ID</param>
    /// <param name="reason">撤销原因（用于审计日志上下文）</param>
    /// <returns>实际被撤销的 token 数量（已撤销的不会重复计数）</returns>
    int RevokeFamily(string familyId, string reason);

    /// <summary>
    /// 清理过期的 RefreshToken
    /// </summary>
    int CleanExpiredTokens();
}
