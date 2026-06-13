namespace Wms.Core.WebApi.Configuration;

/// <summary>
/// 健康检查配置选项
/// </summary>
public class HealthCheckOptions
{
    public const string SectionName = "HealthChecks";

    /// <summary>
    /// 是否启用健康检查
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 磁盘空间检查配置
    /// </summary>
    public DiskSpaceCheckOptions DiskSpace { get; set; } = new();

    /// <summary>
    /// Redis 检查配置
    /// </summary>
    public RedisCheckOptions Redis { get; set; } = new();

    /// <summary>
    /// WCS 检查配置
    /// </summary>
    public WcsCheckOptions Wcs { get; set; } = new();
}

/// <summary>
/// 磁盘空间检查选项
/// </summary>
public class DiskSpaceCheckOptions
{
    /// <summary>
    /// 是否启用磁盘空间检查
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最小可用空间（GB）
    /// </summary>
    public int MinFreeSpaceGb { get; set; } = 5;

    /// <summary>
    /// 最小可用空间百分比
    /// </summary>
    public int MinFreeSpacePercent { get; set; } = 10;
}

/// <summary>
/// Redis 检查选项
/// </summary>
public class RedisCheckOptions
{
    /// <summary>
    /// 是否启用 Redis 检查
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最大响应时间（毫秒）
    /// </summary>
    public int MaxResponseTimeMs { get; set; } = 1000;
}

/// <summary>
/// WCS 检查选项
/// </summary>
public class WcsCheckOptions
{
    /// <summary>
    /// 是否启用 WCS 检查
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}
