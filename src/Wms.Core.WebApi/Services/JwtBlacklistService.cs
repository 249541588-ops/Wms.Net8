using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 基于 <see cref="IDistributedCache"/> 的 JWT 黑名单服务实现。
/// </summary>
/// <remarks>
/// 实现要点：
/// - 直接使用 IDistributedCache（绕过 ICacheService 的 JSON 序列化），仅存储短字符串
/// - Key 格式：<c>jwt:blacklist:{jti}</c>（搭配 Redis InstanceName "Wms:" 前缀避免与其它键冲突）
/// - TTL 精确到秒：token 自然过期后缓存键自动清理，无需主动维护
/// - fail-open：异常不向上抛，避免 Redis 故障锁死认证流程
/// </remarks>
public class JwtBlacklistService : IJwtBlacklistService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<JwtBlacklistService> _logger;

    /// <summary>
    /// 黑名单键前缀，与 <see cref="IsRevokedAsync"/> / <see cref="RevokeAsync"/> 共享。
    /// </summary>
    public const string KeyPrefix = "jwt:blacklist:";

    /// <summary>
    /// 初始化 <see cref="JwtBlacklistService"/> 的新实例。
    /// </summary>
    /// <param name="cache">分布式缓存（Redis 启用时为 Redis，否则为内存回退）</param>
    /// <param name="logger">日志记录器</param>
    public JwtBlacklistService(IDistributedCache cache, ILogger<JwtBlacklistService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string jti, DateTime expiresAtUtc, string reason)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            _logger.LogWarning("RevokeAsync 收到空 jti，跳过");
            return;
        }

        // token 已自然过期，无需占用缓存空间
        var ttl = expiresAtUtc - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            _logger.LogDebug("JWT 已自然过期，跳过黑名单写入。Jti={Jti}", jti);
            return;
        }

        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };
            // 缓存值仅用于事后审计（如运维查 Redis 时能看到撤销原因），不参与判断逻辑
            await _cache.SetStringAsync(KeyPrefix + jti, reason ?? string.Empty, options);

            _logger.LogInformation(
                "[R503] JWT 加入黑名单。Jti={Jti}, TTL={Ttl:g}, Reason={Reason}",
                jti, ttl, reason);
        }
        catch (Exception ex)
        {
            // fail-open：Redis 故障时不阻塞业务（登出/改密仍返回成功）
            // 副作用：被吊销的 token 仍有效至自然过期，运维需监控此告警
            _logger.LogError(ex,
                "[R503][FAIL-OPEN] JWT 加入黑名单失败（缓存后端可能不可用）。Jti={Jti}, Reason={Reason}",
                jti, reason);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsRevokedAsync(string jti)
    {
        if (string.IsNullOrWhiteSpace(jti)) return false;

        try
        {
            var value = await _cache.GetStringAsync(KeyPrefix + jti);
            return value != null;
        }
        catch (Exception ex)
        {
            // fail-open：缓存后端故障时放行 token，避免锁死整个系统
            // 安全代价：故障期间黑名单失效，被吊销的 token 仍可用至自然过期
            _logger.LogError(ex,
                "[R503][FAIL-OPEN] 检查 JWT 黑名单失败（缓存后端可能不可用），本次放行。Jti={Jti}",
                jti);
            return false;
        }
    }
}
