namespace Wms.Core.WebApi.Services;

/// <summary>
/// JWT 黑名单服务接口（基于分布式缓存实现）
/// </summary>
/// <remarks>
/// 用途：
/// 解决"JWT 无状态导致 Logout / 改密 / 强制下线后 token 仍有效至自然过期"的问题。
/// 通过将 token 的 jti（JWT ID）写入分布式缓存黑名单，使其在 JwtBearer
/// OnTokenValidated 事件中被拒绝。
///
/// 失效策略：
/// - 黑名单条目携带 TTL = (JWT 过期时间 - 当前时间)，token 自然过期后自动清理。
/// - Redis 不可用时采用 <b>fail-open</b> 策略：登出/改密操作只记录 Error 日志
///   不阻塞业务；后续请求的 IsRevokedAsync 返回 false（放行）。
///   这是为了避免 Redis 故障锁死整个认证系统，安全代价是故障期间被吊销的
///   token 仍可用至自然过期（默认 60 分钟），运维需监控 [FAIL-OPEN] 告警。
///
/// 配置依赖：
/// - Redis:Enabled=true 时使用真正的分布式缓存（多实例共享黑名单）
/// - Redis:Enabled=false 时回退到内存缓存（IDistributedCache 的内存实现），
///   仅适合单实例部署，多实例下黑名单不共享
/// </remarks>
public interface IJwtBlacklistService
{
    /// <summary>
    /// 将 JWT 加入黑名单（使其立即失效，直到自然过期时间）。
    /// </summary>
    /// <param name="jti">JWT 的 jti claim 值（唯一标识）</param>
    /// <param name="expiresAtUtc">JWT 的自然过期时间（UTC），用于计算 TTL</param>
    /// <param name="reason">撤销原因（写入缓存值与日志，便于审计，例如 "用户登出" / "修改密码"）</param>
    /// <returns>异步任务</returns>
    Task RevokeAsync(string jti, DateTime expiresAtUtc, string reason);

    /// <summary>
    /// 检查指定 JWT 是否已被吊销（每次 JWT 鉴权都会调用，需高效）。
    /// </summary>
    /// <param name="jti">JWT 的 jti claim 值</param>
    /// <returns>true=已吊销，鉴权应拒绝；false=有效（或缓存不可用时 fail-open 放行）</returns>
    Task<bool> IsRevokedAsync(string jti);
}
