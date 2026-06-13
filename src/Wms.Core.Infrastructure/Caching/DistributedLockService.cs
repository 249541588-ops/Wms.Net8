using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Infrastructure.Caching;

/// <summary>
/// 分布式锁服务实现（基于 Redis SET NX EX + Lua 脚本释放）
/// </summary>
public class DistributedLockService : IDistributedLockService
{
    private readonly IDatabase _redis;
    private readonly ILogger<DistributedLockService> _logger;
    private const string LockKeyPrefix = "wms:lock:";

    /// <summary>
    /// Lua 脚本：原子性释放锁（仅当令牌匹配时才删除）
    /// </summary>
    private const string UnlockLuaScript = @"
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
    ";

    /// <summary>
    /// 初始化分布式锁服务
    /// </summary>
    public DistributedLockService(IConnectionMultiplexer redis, ILogger<DistributedLockService> logger)
    {
        _redis = redis?.GetDatabase() ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取分布式锁
    /// </summary>
    public Task<bool> LockTakeAsync(string key, TimeSpan expiry, out string token)
    {
        token = Guid.NewGuid().ToString("N");
        var fullKey = $"{LockKeyPrefix}{key}";
        var result = _redis.StringSet(fullKey, token, expiry, when: When.NotExists);
        return Task.FromResult(result);
    }

    /// <summary>
    /// 释放分布式锁（Lua 原子操作）
    /// </summary>
    public async Task<bool> LockReleaseAsync(string key, string token)
    {
        try
        {
            var fullKey = $"{LockKeyPrefix}{key}";
            var result = (int)await _redis.ScriptEvaluateAsync(UnlockLuaScript, new RedisKey[] { fullKey }, new RedisValue[] { token });
            return result == 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[分布式锁] 释放失败 key={Key}", key);
            return false;
        }
    }

    /// <summary>
    /// 在分布式锁内执行操作
    /// </summary>
    public async Task<bool> ExecuteWithLockAsync(string key, TimeSpan expiry, Func<Task> action)
    {
        if (!await LockTakeAsync(key, expiry, out var token))
        {
            _logger.LogWarning("[分布式锁] 获取失败 key={Key}", key);
            return false;
        }

        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[分布式锁] 执行异常 key={Key}", key);
            return false;
        }
        finally
        {
            await LockReleaseAsync(key, token);
        }
    }
}
