using Wms.Core.Domain.Entities.Transport;

namespace Wms.Core.Application.Ports;

/// <summary>
/// 任务完成处理器接口（策略模式 — 完成阶段）
/// </summary>
/// <remarks>
/// WCS 完成搬运后，不同任务类型需要不同的业务处理逻辑。
/// 通过策略模式解耦，WcsTaskSyncService 按 task_type 匹配对应 Handler。
/// </remarks>
public interface ITaskCompletionHandler
{
    /// <summary>
    /// 处理的任务类型列表（对应 wcs_tasks.task_type）
    /// </summary>
    string[] TaskTypes { get; }

    /// <summary>
    /// 任务完成后的业务处理
    /// </summary>
    /// <param name="wcsTask">WCS 完成的任务数据</param>
    Task HandleAsync(WcsTask wcsTask);
}
