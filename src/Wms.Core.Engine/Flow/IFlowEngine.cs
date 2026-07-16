using Wms.Core.Domain.Entities.Flow;
using Wms.Core.Domain.Utilities.Response;

namespace Wms.Core.Engine;

/// <summary>
/// 流程引擎接口
/// </summary>
public interface IFlowEngine
{
    /// <summary>
    /// 匹配流程模板（带缓存）
    /// </summary>
    /// <param name="requestType">请求类型（入库/出库/移库）</param>
    /// <param name="phase">流程阶段（Request/Completion）</param>
    /// <param name="warehouseId">仓库 ID（可选，用于条件匹配）</param>
    /// <param name="locationTag">货位标签（可选）</param>
    /// <returns>匹配的流程模板，无匹配返回 null</returns>
    Task<FlowTemplate?> MatchTemplateAsync(string requestType, string phase, int? warehouseId = null, string? locationTag = null);

    /// <summary>
    /// 执行流程（WCS 请求阶段）
    /// </summary>
    /// <param name="template">流程模板</param>
    /// <param name="context">流程上下文</param>
    /// <returns>WCS 返回结果</returns>
    Task<WcsResult> ExecuteAsync(FlowTemplate template, FlowContext context);

    /// <summary>
    /// 执行流程（任务完成阶段，无 WCS 返回值）
    /// </summary>
    /// <param name="template">流程模板</param>
    /// <param name="context">流程上下文</param>
    Task ExecuteCompletionAsync(FlowTemplate template, FlowContext context);

    /// <summary>
    /// 清除模板缓存（模板修改后调用）
    /// </summary>
    void ClearCache();
}
