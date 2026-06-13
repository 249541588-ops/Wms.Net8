using Microsoft.Extensions.Logging;

namespace Wms.Core.Application.Jobs;

/// <summary>
/// 超时订单清理定时任务
/// </summary>
public class TimeoutOrderCleanupJob
{
    private readonly ILogger<TimeoutOrderCleanupJob> _logger;

    /// <summary>
    /// 初始化超时订单清理任务
    /// </summary>
    public TimeoutOrderCleanupJob(ILogger<TimeoutOrderCleanupJob> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 执行超时订单清理（Hangfire 调用入口）
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("[Hangfire] 超时订单清理任务开始执行 - {Time}", DateTime.Now);

        try
        {
            // TODO: 具体清理逻辑待后续 WMS 业务开发时填充
            // 1. 查询超时未完成的入库/出库订单
            // 2. 自动关闭或标记为超时
            // 3. 释放已分配的库存

            await Task.CompletedTask;
            _logger.LogInformation("[Hangfire] 超时订单清理任务执行完成 - {Time}", DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Hangfire] 超时订单清理任务执行失败");
            throw;
        }
    }
}
