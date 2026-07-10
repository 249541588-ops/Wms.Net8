using System.Threading.Channels;
using Wms.Core.Application.Ports;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// Channel 实现的后台任务队列
/// </summary>
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _channel;

    public BackgroundTaskQueue(int capacity = 1000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<Func<CancellationToken, Task>>(options);
    }

    public async ValueTask QueueAsync(Func<CancellationToken, Task> workItem)
    {
        if (workItem == null) throw new ArgumentNullException(nameof(workItem));
        await _channel.Writer.WriteAsync(workItem);
    }

    /// <summary>
    /// 读取队列中的任务（供 HostedService 使用）
    /// </summary>
    public IAsyncEnumerable<Func<CancellationToken, Task>> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
