namespace Wms.Core.Domain.Requests;

//// <summary>
/// 创建任务请求体
/// </summary>
public class CreateJobRequest
{
    /// <summary>
    /// 执行模式：internal 或 http-call
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Cron 表达式
    /// </summary>
    public string Cron { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// API 地址（internal 模式为方法标识，http-call 模式为 API 路径）
    /// </summary>
    public string? ApiUrl { get; set; }

    /// <summary>
    /// HTTP 方法（仅 http-call 模式）
    /// </summary>
    public string? RequestMethod { get; set; }

    /// <summary>
    /// 请求体 JSON（仅 http-call 模式）
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// 请求头 JSON（仅 http-call 模式）
    /// </summary>
    public string? Headers { get; set; }
}

/// <summary>
/// 修改 Cron 请求体
/// </summary>
public class UpdateCronRequest
{
    public string Cron { get; set; } = string.Empty;
}