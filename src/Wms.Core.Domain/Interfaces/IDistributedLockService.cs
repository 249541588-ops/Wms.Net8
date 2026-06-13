namespace Wms.Core.Domain.Interfaces;

/// <summary>
/// 分布式锁服务接口
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// 获取分布式锁
    /// </summary>
    /// <param name="key">锁键</param>
    /// <param name="expiry">过期时间</param>
    /// <param name="token">锁令牌（用于释放时验证）</param>
    /// <returns>是否获取成功</returns>
    Task<bool> LockTakeAsync(string key, TimeSpan expiry, out string token);

    /// <summary>
    /// 释放分布式锁（使用 Lua 脚本保证原子性）
    /// </summary>
    /// <param name="key">锁键</param>
    /// <param name="token">获取锁时返回的令牌</param>
    /// <returns>是否释放成功</returns>
    Task<bool> LockReleaseAsync(string key, string token);

    /// <summary>
    /// 在分布式锁内执行操作
    /// </summary>
    /// <param name="key">锁键</param>
    /// <param name="expiry">锁过期时间</param>
    /// <param name="action">要执行的操作</param>
    /// <returns>是否获取锁并执行成功</returns>
    Task<bool> ExecuteWithLockAsync(string key, TimeSpan expiry, Func<Task> action);
}
