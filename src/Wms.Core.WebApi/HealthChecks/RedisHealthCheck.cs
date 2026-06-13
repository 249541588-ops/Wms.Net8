using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Diagnostics;
using Wms.Core.WebApi.Configuration;

namespace Wms.Core.WebApi.HealthChecks;

/// <summary>
/// Redis 缓存健康检查
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(
        IConnectionMultiplexer? redis,
        IOptions<RedisOptions> options,
        ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 如果未启用 Redis，返回降级状态
            if (!_options.Enabled || _redis == null)
            {
                data["status"] = "disabled";
                data["message"] = "Redis is disabled, using in-memory cache";
                return HealthCheckResult.Degraded("Redis cache is disabled", data: data);
            }

            // 检查 Redis 连接
            if (!_redis.IsConnected)
            {
                data["status"] = "disconnected";
                data["message"] = "Redis is not connected";
                return HealthCheckResult.Unhealthy("Redis is not connected", data: data);
            }

            // 测试 Redis 命令
            var db = _redis.GetDatabase();
            var pingTime = await db.PingAsync();

            data["status"] = "connected";
            data["ping_time_ms"] = pingTime.TotalMilliseconds;
            data["connected_clients"] = _redis.GetEndPoints().Count();

            stopwatch.Stop();
            data["response_time_ms"] = stopwatch.ElapsedMilliseconds;

            // 根据 ping 时间判断健康状态
            if (pingTime.TotalMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded("Redis response time is slow", data: data);
            }

            return HealthCheckResult.Healthy("Redis is operating normally", data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");
            stopwatch.Stop();
            data["error"] = ex.Message;
            data["response_time_ms"] = stopwatch.ElapsedMilliseconds;
            return HealthCheckResult.Unhealthy("Redis health check failed", exception: ex, data: data);
        }
    }
}
