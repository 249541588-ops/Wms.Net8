using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Wms.Core.WebApi.Configuration;
using Wms.Core.Application.Ports;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 分布式缓存服务实现
/// </summary>
public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly RedisOptions _options;

    public DistributedCacheService(
        IDistributedCache cache,
        ILogger<DistributedCacheService> logger,
        IOptions<RedisOptions> options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var fullKey = GetKey(key);
            var bytes = await _cache.GetAsync(fullKey, cancellationToken);

            if (bytes == null)
            {
                return null;
            }

            var json = System.Text.Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key {Key}: {Message}", key, ex.Message);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var fullKey = GetKey(key);
            var json = JsonSerializer.Serialize(value);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes)
            };

            await _cache.SetAsync(fullKey, bytes, options, cancellationToken);
            _logger.LogDebug("Cache set for key {Key} with expiration {Expiration} minutes", key, options.AbsoluteExpirationRelativeToNow?.TotalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key}: {Message}", key, ex.Message);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullKey = GetKey(key);
            await _cache.RemoveAsync(fullKey, cancellationToken);
            _logger.LogDebug("Cache removed for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key {Key}: {Message}", key, ex.Message);
        }
    }

    public async Task RefreshAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullKey = GetKey(key);
            await _cache.RefreshAsync(fullKey, cancellationToken);
            _logger.LogDebug("Cache refreshed for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing cache key {Key}: {Message}", key, ex.Message);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await GetAsync<object>(key, cancellationToken);
            return value != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache key {Key}: {Message}", key, ex.Message);
            return false;
        }
    }

    private string GetKey(string key)
    {
        return $"{_options.InstanceName}{key}";
    }
}
