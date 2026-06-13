using Wms.Core.Domain.Entities.Transport;

namespace Wms.Core.Domain.Interfaces;

/// <summary>
/// WCS 任务通信适配器接口（支持 Database / Http 两种模式）
/// </summary>
public interface IWcsTaskBridge
{
    /// <summary>
    /// 下发任务到 WCS（写入中间表或调用 HTTP API）
    /// </summary>
    Task SendTaskAsync(TransTask transTask);

    /// <summary>
    /// 轮询 WCS 任务状态变更
    /// </summary>
    Task<IReadOnlyList<WcsTask>> PollStatusChangesAsync();
}
