using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Infrastructure.Caching;

/// <summary>
/// 库存缓存服务实现（Cache-Aside 模式）
/// </summary>
public class InventoryCacheService : IInventoryCacheService
{
    private readonly IDatabase _redis;
    private readonly ILogger<InventoryCacheService> _logger;
    private const string KeyPrefix = "wms:stock:";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 初始化库存缓存服务
    /// </summary>
    /// <param name="redis">Redis 连接</param>
    /// <param name="logger">日志</param>
    public InventoryCacheService(IConnectionMultiplexer redis, ILogger<InventoryCacheService> logger)
    {
        _redis = redis?.GetDatabase() ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static string BuildKey(int locationId, int materialId)
        => $"{KeyPrefix}{locationId}:{materialId}";

    /// <summary>
    /// 获取库存数量
    /// </summary>
    public async Task<decimal?> GetStockQtyAsync(int locationId, int materialId)
    {
        try
        {
            var key = BuildKey(locationId, materialId);
            var value = await _redis.StringGetAsync(key);

            if (value.IsNullOrEmpty)
                return null;

            return (decimal?)Convert.ToDouble(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[库存缓存] 获取失败 key={Key}", BuildKey(locationId, materialId));
            return null;
        }
    }

    /// <summary>
    /// 设置库存数量到缓存
    /// </summary>
    public async Task SetStockQtyAsync(int locationId, int materialId, decimal qty, TimeSpan? expiration = null)
    {
        try
        {
            var key = BuildKey(locationId, materialId);
            await _redis.StringSetAsync(key, (RedisValue)(double)qty, expiration ?? DefaultExpiration);
            _logger.LogDebug("[库存缓存] 设置 key={Key}, qty={Qty}", key, qty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[库存缓存] 设置失败 key={Key}", BuildKey(locationId, materialId));
        }
    }

    /// <summary>
    /// 删除库存缓存
    /// </summary>
    public async Task RemoveStockAsync(int locationId, int materialId)
    {
        try
        {
            var key = BuildKey(locationId, materialId);
            await _redis.KeyDeleteAsync(key);
            _logger.LogDebug("[库存缓存] 删除 key={Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[库存缓存] 删除失败 key={Key}", BuildKey(locationId, materialId));
        }
    }

    /// <summary>
    /// 按库位删除所有库存缓存
    /// </summary>
    public async Task RemoveByLocationAsync(int locationId)
    {
        try
        {
            var pattern = $"{KeyPrefix}{locationId}:*";
            var server = _redis.Multiplexer.GetServer(_redis.Multiplexer.GetEndPoints()[0]);
            var keys = server.Keys(pattern: pattern).ToArray();

            if (keys.Length > 0)
            {
                await _redis.KeyDeleteAsync(keys);
                _logger.LogDebug("[库存缓存] 按库位删除 {Count} 个缓存, locationId={LocationId}", keys.Length, locationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[库存缓存] 按库位删除失败, locationId={LocationId}", locationId);
        }
    }
}
