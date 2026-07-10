using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Enums;
using Wms.Core.Application.Ports;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Application.Ports;

namespace Wms.Core.Infrastructure.Clients;

/// <summary>
/// WCS 任务通信适配器 — 数据库模式（共享 ctask 数据库中间表）
/// </summary>
public class DatabaseWcsTaskBridge : IWcsTaskBridge
{
    private readonly ICtaskDbService _ctaskDb;
    private readonly WmsDbContext _db;
    private readonly ILogger<DatabaseWcsTaskBridge> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundTaskQueue _taskQueue;

    /// <summary>
    /// 初始化数据库模式适配器
    /// </summary>
    public DatabaseWcsTaskBridge(
        ICtaskDbService ctaskDb,
        WmsDbContext db,
        ILogger<DatabaseWcsTaskBridge> logger,
        IServiceScopeFactory scopeFactory,
        IBackgroundTaskQueue taskQueue)
    {
        _ctaskDb = ctaskDb ?? throw new ArgumentNullException(nameof(ctaskDb));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _taskQueue = taskQueue ?? throw new ArgumentNullException(nameof(taskQueue));
    }

    /// <summary>
    /// 下发任务到 WCS（写入 ctask.wcs_tasks 中间表）
    /// </summary>
    public async Task SendTaskAsync(TransTask transTask)
    {
        // 重新加载并 Include 导航属性
        var task = await _db.TransTasks
            .Include(t => t.StartLocation)
            .Include(t => t.EndLocation)
            .Include(t => t.Unitload)
            .FirstOrDefaultAsync(t => t.Id == transTask.Id)
            ?? transTask;

        if (task == transTask)
        {
            _logger.LogWarning("[WcsBridge] 未重新加载 TransTask（可能未持久化），使用原始对象: TaskCode={TaskCode}", transTask.TaskCode);
        }

        var wcsTask = new WcsTask
        {
            TaskCode = task.TaskCode ?? task.Id.ToString(),
            TaskType = task.TaskType ?? string.Empty,
            ContCode = task.Unitload?.ContainerCode ?? string.Empty,
            ContType = task.Unitload?.ContainerSpecification,
            StartLoc = task.StartLocation?.LocationCode ?? string.Empty,
            EndLoc = task.EndLocation?.LocationCode ?? string.Empty,
            Prio = 0,
            SentAt = DateTime.Now,
            WmsState = TaskInfoWmsStates.Sent,
            WcsState = TaskInfoWcsStates.Unread,
            UpdatedAt = DateTime.Now,
            Warehouse = task.WareHouse,
            LocationGroup = task.LocationGroup,
            WmsNote = task.Comment,
            Ex1 = task.Ext1,
            Ex2 = task.Ext2
        };

        await _ctaskDb.WriteTaskAsync(wcsTask);
        _logger.LogInformation("[WcsBridge] 任务已下发到 wcs_tasks: {TaskCode}", wcsTask.TaskCode);

        // 异步记录出站接口日志
        _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync("WMS", "SendTask", task.EndLocation?.LocationCode ?? string.Empty, task.Unitload?.ContainerCode, JsonConvert.SerializeObject(wcsTask), true));
    }

    /// <summary>
    /// 轮询 WCS 任务状态变更
    /// </summary>
    public async Task<IReadOnlyList<WcsTask>> PollStatusChangesAsync()
    {
        // 增量同步：查询最近 2 分钟内更新的未完成任务
        var since = DateTime.Now.AddMinutes(-2);
        var tasks = await _ctaskDb.ReadTasksUpdatedAfterAsync(since);

        // 过滤出 WCS 端状态已变更的任务（执行中/已完成/失败）
        var changed = tasks
            .Where(t => t.WcsState != TaskInfoWcsStates.Unread)
            .ToList();

        if (changed.Count > 0)
        {
            _logger.LogDebug("[WcsBridge] 轮询到 {Count} 个状态变更任务", changed.Count);
        }

        return changed;
    }

    /// <summary>
    /// 删除已下发的任务（用于回滚）
    /// </summary>
    public async Task<bool> DeleteTaskAsync(string taskCode)
    {
        var affected = await _ctaskDb.DeleteTaskAsync(taskCode);
        _logger.LogInformation("[WcsBridge] 删除 wcs_tasks: TaskCode={TaskCode}, Affected={Affected}", taskCode, affected);
        return affected > 0;
    }

    private async Task SaveInterfaceLogAsync(string source, string endpoint, string? locationCode, string? containerCode, string requestBody, bool success)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var logDb = scope.ServiceProvider.GetRequiredService<WmsLogDbContext>();
            logDb.InterfaceLogs.Add(new InterfaceLog
            {
                Source = source,
                Endpoint = endpoint,
                LocationCode = locationCode,
                ContainerCode = containerCode,
                RequestBody = requestBody?.Length > 8000 ? requestBody[..8000] : requestBody ?? "",
                Success = success,
                CreatedTime = DateTime.UtcNow,
            });
            await logDb.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WcsBridge] 写入接口日志失败: {Message}", ex.Message);
        }
    }
}
