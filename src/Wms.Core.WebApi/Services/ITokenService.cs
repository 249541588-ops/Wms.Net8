using Wms.Core.WebApi.Models;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// Token 服务接口
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// 生成 JWT Token
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="username">用户名</param>
    /// <param name="role">角色</param>
    /// <param name="permissions">权限列表</param>
    /// <returns>Token、过期时间和 Token ID</returns>
    (string token, DateTime expiration, string tokenId) GenerateToken(string userId, string username, string role, string[] permissions);

    /// <summary>
    /// 生成刷新 Token
    /// </summary>
    /// <returns>刷新 Token</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// 验证 Token 并提取用户信息
    /// </summary>
    /// <param name="token">JWT Token</param>
    /// <returns>用户信息</returns>
    UserInfo? ValidateToken(string token);

    /// <summary>
    /// 从 Token 中获取用户 ID
    /// </summary>
    /// <param name="token">JWT Token</param>
    /// <returns>用户 ID</returns>
    string? GetUserIdFromToken(string token);
}
