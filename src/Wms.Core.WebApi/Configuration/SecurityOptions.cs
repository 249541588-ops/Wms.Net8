namespace Wms.Core.WebApi.Configuration;

/// <summary>
/// 安全配置选项
/// </summary>
public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// 是否强制 HTTPS
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// 允许的跨域来源列表
    /// </summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
