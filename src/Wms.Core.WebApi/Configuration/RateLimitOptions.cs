namespace Wms.Core.WebApi.Configuration;

/// <summary>
/// 速率限制配置选项
/// </summary>
public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>
    /// 是否启用速率限制
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// 全局限制（每分钟请求数）
    /// </summary>
    public int GlobalLimit { get; set; } = 600;

    /// <summary>
    /// 全局滑动窗口（秒）
    /// </summary>
    public int GlobalSlidingWindow { get; set; } = 60;

    /// <summary>
    /// 每个客户端限制（每分钟请求数）
    /// </summary>
    public int PerClientLimit { get; set; } = 200;

    /// <summary>
    /// 每个客户端滑动窗口（秒）
    /// </summary>
    public int PerClientSlidingWindow { get; set; } = 60;

    /// <summary>
    /// 端点特定规则
    /// </summary>
    public List<EndpointRateLimitRule> EndpointRules { get; set; } = new();
}

/// <summary>
/// 端点速率限制规则
/// </summary>
public class EndpointRateLimitRule
{
    /// <summary>
    /// 端点路径（如：api/auth/login）
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 方法（GET, POST, PUT, DELETE, *）
    /// </summary>
    public string Method { get; set; } = "*";

    /// <summary>
    /// 限制周期（秒）
    /// </summary>
    public int Period { get; set; } = 60;

    /// <summary>
    /// 限制请求数
    /// </summary>
    public int Limit { get; set; } = 100;
}
