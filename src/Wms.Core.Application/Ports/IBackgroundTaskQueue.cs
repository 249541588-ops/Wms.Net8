namespace Wms.Core.Application.Ports;

/// <summary>
/// 后台任务队列（基于 Channel，替代 fire-and-forget Task.Run）
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// 将任务加入队列
    /// </summary>
    ValueTask QueueAsync(Func<CancellationToken, Task> workItem);

    /// <summary>
    /// 读取队列中的任务（供 HostedService 使用）
    /// </summary>
    IAsyncEnumerable<Func<CancellationToken, Task>> ReadAllAsync(CancellationToken cancellationToken);
}
