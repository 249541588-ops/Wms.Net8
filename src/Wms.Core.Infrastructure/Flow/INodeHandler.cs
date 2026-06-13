using System.Text.Json;

namespace Wms.Core.Engine;

/// <summary>
/// 节点处理器接口
/// </summary>
public interface INodeHandler
{
    /// <summary>
    /// 节点类型标识（如 "FindUnitload"）
    /// </summary>
    string NodeType { get; }

    /// <summary>
    /// 节点显示名称（如 "查托盘"）
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 节点分类（基础/数据查询/状态更新/业务逻辑/外部交互/数据持久化）
    /// </summary>
    string Category { get; }

    /// <summary>
    /// 节点功能描述（用于前端提示）
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 节点配置 JSON Schema（用于前端动态表单，可选）
    /// </summary>
    string? ConfigSchema { get; }

    /// <summary>
    /// 执行节点逻辑
    /// </summary>
    /// <param name="context">流程上下文</param>
    /// <param name="configJson">节点配置 JSON</param>
    /// <returns>节点执行结果</returns>
    Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson);
}

/// <summary>
/// 节点执行结果
/// </summary>
public class NodeResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// 是否跳过
    /// </summary>
    public bool Skipped { get; set; }

    /// <summary>
    /// 是否停止后续节点执行
    /// </summary>
    public bool Stop { get; set; }

    /// <summary>
    /// 失败消息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// WCS 返回结果码（仅 WCS 请求阶段使用）
    /// </summary>
    public string? ResultCode { get; set; }

    /// <summary>
    /// WCS 返回数据量（仅 WCS 请求阶段使用）
    /// </summary>
    public int? ResultData { get; set; }

    /// <summary>
    /// 节点输出数据（合并到 FlowContext.Data）
    /// </summary>
    public Dictionary<string, object?>? Output { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static NodeResult Ok(Dictionary<string, object?>? output = null)
        => new() { Success = true, Output = output };

    /// <summary>
    /// 创建跳过结果
    /// </summary>
    public static NodeResult Skip(string? message = null)
        => new() { Skipped = true, Message = message };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static NodeResult Fail(string message)
        => new() { Success = false, Stop = true, Message = message };

    /// <summary>
    /// 创建 WCS 失败结果（带 ResultCode 和 ResultData）
    /// </summary>
    public static NodeResult WcsFail(string message, string resultCode, int resultData)
        => new() { Success = false, Stop = true, Message = message, ResultCode = resultCode, ResultData = resultData };
}
