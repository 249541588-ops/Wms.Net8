namespace Wms.Core.WebApi.Configuration;

/// <summary>
/// JWT 配置选项
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// 密钥（用于签名 Token）
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// 签发者
    /// </summary>
    public string Issuer { get; set; } = "Wms.Core.WebApi";

    /// <summary>
    /// 受众者
    /// </summary>
    public string Audience { get; set; } = "Wms.Client";

    /// <summary>
    /// 密钥（至少 32 字符）
    /// </summary>
    public string SecretKey { get; set; } = "CHANGE_ME_USE_USER_SECRETS_OR_ENVIRONMENT_VARIABLE_32_CHARS_MIN";

    /// <summary>
    /// Token 过期时间（分钟）
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// 刷新 Token 过期时间（分钟）
    /// </summary>
    public int RefreshExpirationDays { get; set; } = 7;
}
