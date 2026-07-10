using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.Ports;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 后台任务队列消费者（HostedService，有序处理队列中的任务）
/// </summary>
public class BackgroundTaskQueueHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<BackgroundTaskQueueHostedService> _logger;

    public BackgroundTaskQueueHostedService(
        IBackgroundTaskQueue taskQueue,
        ILogger<BackgroundTaskQueueHostedService> logger)
    {
        _taskQueue = taskQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workItem in _taskQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "后台任务执行失败");
            }
        }
    }
}
