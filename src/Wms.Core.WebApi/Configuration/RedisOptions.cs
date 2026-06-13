namespace Wms.Core.WebApi.Configuration;

/// <summary>
/// Redis 缓存配置选项
/// </summary>
public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis 连接字符串
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 实例名称（用于键前缀）
    /// </summary>
    public string InstanceName { get; set; } = "Wms:";

    /// <summary>
    /// 是否启用 Redis
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 默认过期时间（分钟）
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 30;
}
