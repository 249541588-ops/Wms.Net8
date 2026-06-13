using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Interfaces;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Hubs;

namespace Wms.Core.WebApi.Services.Wcs;

/// <summary>
/// WCS 任务同步服务
/// </summary>
/// <remarks>
/// 负责：
/// 1. 下发任务到 WCS（通过 Bridge 写入中间表）
/// 2. 轮询 WCS 状态变更
/// 3. SignalR 推送前端通知
/// </remarks>
public class WcsTaskSyncService
{
    private readonly IWcsTaskBridge _bridge;
    private readonly WmsDbContext _db;
    private readonly IHubContext<WmsHub> _hub;
    private readonly IEnumerable<ITaskCompletionHandler> _completionHandlers;
    private readonly IFlowEngine _flowEngine;
    private readonly ILogger<WcsTaskSyncService> _logger;

    /// <summary>
    /// 初始化 WCS 任务同步服务
    /// </summary>
    public WcsTaskSyncService(
        IWcsTaskBridge bridge,
        WmsDbContext db,
        IHubContext<WmsHub> hub,
        IEnumerable<ITaskCompletionHandler> completionHandlers,
        IFlowEngine flowEngine,
        ILogger<WcsTaskSyncService> logger)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _completionHandlers = completionHandlers ?? throw new ArgumentNullException(nameof(completionHandlers));
        _flowEngine = flowEngine ?? throw new ArgumentNullException(nameof(flowEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 下发任务到 WCS
    /// </summary>
    public async Task SendTaskToWcsAsync(TransTask transTask)
    {
        try
        {
            await _bridge.SendTaskAsync(transTask);

            transTask.WasSentToWcs = true;
            transTask.SentToWcsAt = DateTime.Now;
            _db.Entry(transTask).Property(t => t.WasSentToWcs).IsModified = true;
            _db.Entry(transTask).Property(t => t.SentToWcsAt).IsModified = true;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WcsSync] 任务下发失败: TaskCode={TaskCode}", transTask.TaskCode);
        }
    }

    /// <summary>
    /// 同步 WCS 任务状态（Hangfire 轮询调用）
    /// </summary>
    public async Task SyncStatusAsync()
    {
        _logger.LogDebug("[WcsSync] 开始同步 WCS 任务状态");

        try
        {
            var changedTasks = await _bridge.PollStatusChangesAsync();
            if (changedTasks.Count == 0)
            {
                _logger.LogDebug("[WcsSync] 无状态变更任务");
                return;
            }

            foreach (var wcsTask in changedTasks)
            {
                await SyncSingleTaskAsync(wcsTask);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WcsSync] 同步 WCS 状态失败");
            throw;
        }
    }

    /// <summary>
    /// 同步单个任务
    /// </summary>
    private async Task SyncSingleTaskAsync(WcsTask wcsTask)
    {
        var transTask = await _db.TransTasks
            .Include(t => t.Unitload)
            .Include(t => t.StartLocation)
            .Include(t => t.EndLocation)
            .FirstOrDefaultAsync(t => t.TaskCode == wcsTask.TaskCode);

        if (transTask == null)
        {
            _logger.LogWarning("[WcsSync] 未找到 TransTask: TaskCode={TaskCode}", wcsTask.TaskCode);
            return;
        }

        _logger.LogInformation("[WcsSync] 同步完成: TransTaskId={Id}, WcsState={State}",
            transTask.Id, wcsTask.WcsState);

        // 按任务类型分发到对应 Handler（已完成/失败时触发）
        if (TaskInfoWcsStates.Finished.Contains(wcsTask.WcsState))
        {
            // 优先：流程引擎（完成阶段）
            var template = await _flowEngine.MatchTemplateAsync(
                wcsTask.TaskType ?? "", Cst.PhaseCompletion);

            if (template != null)
            {
                try
                {
                    var flowContext = new FlowContext(_db)
                    {
                        Phase = Cst.PhaseCompletion,
                        Unitload = transTask.Unitload,
                        StartLocation = transTask.StartLocation,
                        TargetLocation = transTask.EndLocation,
                        WcsTask = wcsTask,
                        TransTask = transTask,
                        IsCancelled = wcsTask.WcsState != TaskInfoWcsStates.Completed,
                        BusinessType = "TaskCompletion",
                        BusinessId = wcsTask.TaskCode
                    };

                    // 加载额外的 Unitload（Ext2 存储了除 UnitloadId 外的其他 UnitloadId）
                    if (!string.IsNullOrEmpty(transTask.Ext2))
                    {
                        var additionalIds = transTask.Ext2.Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(idStr => int.TryParse(idStr, out var id) ? id : 0)
                            .Where(id => id > 0)
                            .ToList();
                        if (additionalIds.Count > 0)
                        {
                            var additionalUnitloads = await _db.Unitloads
                                .Include(u => u.UnitloadItems)
                                .Where(u => additionalIds.Contains(u.UnitloadId))
                                .ToListAsync();
                            if (additionalUnitloads.Count > 0)
                                flowContext.Data["AdditionalUnitloads"] = additionalUnitloads;
                        }
                    }

                    await _flowEngine.ExecuteCompletionAsync(template, flowContext);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WcsSync] 流程引擎完成阶段执行失败: TaskCode={TaskCode}",
                        wcsTask.TaskCode);
                }
            }
            else
            {
                // 后备：硬编码 Handler
                var handler = _completionHandlers.FirstOrDefault(h => h.TaskType == wcsTask.TaskType);
                if (handler != null)
                {
                    try
                    {
                        await handler.HandleAsync(wcsTask);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[WcsSync] 任务完成处理器执行失败: TaskCode={TaskCode}, Handler={HandlerType}",
                            wcsTask.TaskCode, handler.TaskType);
                    }
                }
                else
                {
                    _logger.LogWarning("[WcsSync] 未找到任务类型处理器: TaskType={TaskType}", wcsTask.TaskType);
                }
            }
        }

        // SignalR 推送前端
        try
        {
            await _hub.Clients.All.SendAsync("ReceiveTaskUpdate",
                new { taskId = transTask.Id, status = wcsTask.WcsState, timestamp = DateTime.Now });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WcsSync] SignalR 推送失败");
        }
    }

    /// <summary>
    /// 补发未成功下发的任务（重试机制）
    /// </summary>
    public async Task RetryUnsentTasksAsync()
    {
        var unsentTasks = await _db.TransTasks
            .Where(t => t.ForWcs == true && t.WasSentToWcs != true)
            .ToListAsync();

        foreach (var task in unsentTasks)
        {
            _logger.LogInformation("[WcsSync] 补发未下发任务: Id={Id}, TaskCode={TaskCode}",
                task.Id, task.TaskCode);
            await SendTaskToWcsAsync(task);
        }
    }
}
