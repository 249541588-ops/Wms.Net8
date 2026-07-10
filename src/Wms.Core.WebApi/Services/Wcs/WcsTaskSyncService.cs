using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Constants;
using Wms.Core.Application.Ports;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Domain.Abstractions;
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
    private readonly ICtaskDbService _ctaskDb;
    private readonly IServiceScopeFactory _scopeFactory;
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
        ICtaskDbService ctaskDb,
        IServiceScopeFactory scopeFactory,
        IHubContext<WmsHub> hub,
        IEnumerable<ITaskCompletionHandler> completionHandlers,
        IFlowEngine flowEngine,
        ILogger<WcsTaskSyncService> logger)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _ctaskDb = ctaskDb ?? throw new ArgumentNullException(nameof(ctaskDb));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
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

            // 内存去重：同一 TaskCode 只处理一次（ctask 中可能有多条状态变更记录）
            var processedTaskCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var wcsTask in changedTasks)
            {
                if (!processedTaskCodes.Add(wcsTask.TaskCode))
                {
                    _logger.LogDebug("[WcsSync] 跳过重复 TaskCode: {TaskCode}", wcsTask.TaskCode);
                    continue;
                }
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
        // 去重：wms_state 已标记为 archived，跳过
        if (wcsTask.WmsState == TaskInfoWmsStates.Archived)
        {
            _logger.LogDebug("[WcsSync] 跳过已处理任务: TaskCode={TaskCode}", wcsTask.TaskCode);
            return;
        }

        // 为每个任务创建独立的 DbContext scope，避免共享 DbContext 状态污染
        using var taskScope = _scopeFactory.CreateScope();
        var taskDb = taskScope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var unitOfWork = taskScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var transTask = await taskDb.TransTasks
            .Include(t => t.Unitload).ThenInclude(u => u.UnitloadItems).ThenInclude(ui => ui.Material)
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
                    var flowContext = new FlowContext(taskDb, unitOfWork)
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
                            var additionalUnitloads = await taskDb.Unitloads
                                .Include(u => u.UnitloadItems).ThenInclude(ui => ui.Material)
                                .Where(u => additionalIds.Contains(u.UnitloadId))
                                .ToListAsync();
                            if (additionalUnitloads.Count > 0)
                                flowContext.Data["AdditionalUnitloads"] = additionalUnitloads;
                        }
                    }

                    await _flowEngine.ExecuteCompletionAsync(template, flowContext);

                    // 处理成功后标记 ctask wms_state 为 archived，防止下次轮询重复处理
                    try
                    {
                        await _ctaskDb.UpdateWmsStateAsync(wcsTask.TaskCode, TaskInfoWmsStates.Archived);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[WcsSync] 更新 ctask wms_state 失败: TaskCode={TaskCode}", wcsTask.TaskCode);
                        // 不影响主流程，下次轮询会重试（内存去重 + archived 检查兜底）
                    }
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
                var handler = _completionHandlers.FirstOrDefault(h => h.TaskTypes.Contains(wcsTask.TaskType));
                if (handler != null)
                {
                    try
                    {
                        await handler.HandleAsync(wcsTask);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[WcsSync] 任务完成处理器执行失败: TaskCode={TaskCode}, Handler={HandlerType}",
                            wcsTask.TaskCode, handler.TaskTypes.FirstOrDefault());
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
