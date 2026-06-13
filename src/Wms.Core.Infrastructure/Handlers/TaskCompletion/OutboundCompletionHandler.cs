using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Interfaces;
using Wms.Core.Infrastructure.Handlers.WcsRequest;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Handlers.TaskCompletion;

/// <summary>
/// 出库完成处理器
/// </summary>
/// <remarks>
/// 完成时：出库流水 → UnitloadOp → 重置状态 → 拆盘归档 → ArchiveTask
/// 取消时：回退 Unitload/Location 状态
/// </remarks>
public class OutboundCompletionHandler : ITaskCompletionHandler
{
    /// <summary>
    /// 任务类型：出库
    /// </summary>
    public string TaskType => Cst.出库;

    private readonly WmsDbContext _db;
    private readonly ILogger<OutboundCompletionHandler> _logger;

    /// <summary>
    /// 初始化出库完成处理器
    /// </summary>
    public OutboundCompletionHandler(WmsDbContext db, ILogger<OutboundCompletionHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 处理出库任务完成/取消
    /// </summary>
    public async Task HandleAsync(WcsTask wcsTask)
    {
        var isCompleted = wcsTask.WcsState == TaskInfoWcsStates.Completed;

        _logger.LogInformation("[TaskCompletion] 出库{Action}: TaskCode={TaskCode}, 容器={ContCode}, 目标库位={EndLoc}",
            isCompleted ? "完成" : "取消",
            wcsTask.TaskCode, wcsTask.ContCode, wcsTask.ActEndLoc ?? wcsTask.EndLoc);

        try
        {
            // 查找 TransTask（Include 导航属性用于流水/拆盘/归档）
            var transTask = await _db.TransTasks
                .Include(t => t.Unitload).ThenInclude(u => u.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                .Include(t => t.StartLocation)
                .Include(t => t.EndLocation)
                .FirstOrDefaultAsync(t => t.TaskCode == wcsTask.TaskCode);

            if (transTask == null)
            {
                _logger.LogWarning("[TaskCompletion] 未找到 TransTask: TaskCode={TaskCode}", wcsTask.TaskCode);
                return;
            }

            if (!isCompleted)
            {
                // 取消：回退 Unitload 和 Location 状态
                await HandleCancelAsync(transTask);

                transTask.Comment = "强制取消";
                transTask.ModifiedTime = DateTime.Now;

                // 任务归档
                LocationAllocator.ArchiveTask(_db, transTask, null, true);

                await _db.SaveChangesAsync();
            }
            else
            {
                // 完成：出库业务处理（含归档、流水、拆盘）
                await HandleCompleteAsync(transTask);
            }

            _logger.LogInformation("[TaskCompletion] 出库{Action}处理成功: TaskCode={TaskCode}",
                !isCompleted ? "取消" : "完成", wcsTask.TaskCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TaskCompletion] 出库{Action}处理失败: TaskCode={TaskCode}",
                !isCompleted ? "取消" : "完成", wcsTask.TaskCode);
            throw;
        }
    }

    /// <summary>
    /// 取消：回退 Unitload 和 Location 状态
    /// </summary>
    private async Task HandleCancelAsync(TransTask transTask)
    {
        if (transTask.Unitload != null)
        {
            transTask.Unitload.BeingMoved = false;
            transTask.Unitload.Allocated = false;
        }

        // 回退额外 Unitload（Ext2 存储了除 UnitloadId 外的其他 UnitloadId）
        if (!string.IsNullOrEmpty(transTask.Ext2))
        {
            var additionalIds = transTask.Ext2.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(idStr => int.TryParse(idStr, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();
            if (additionalIds.Count > 0)
            {
                var additionalUnitloads = await _db.Unitloads
                    .Where(u => additionalIds.Contains(u.UnitloadId))
                    .ToListAsync();
                foreach (var u in additionalUnitloads)
                {
                    u.BeingMoved = false;
                    u.Allocated = false;
                }
            }
        }

        if (transTask.StartLocation != null && transTask.StartLocation.OutboundCount > 0)
        {
            transTask.StartLocation.OutboundCount--;
        }

        // 回退终点 InboundCount（起点≠终点时，如定时器创建的出库任务）
        if (transTask.EndLocation != null
            && transTask.StartLocationId != transTask.EndLocationId
            && transTask.EndLocation.InboundCount > 0)
        {
            transTask.EndLocation.InboundCount--;
        }
    }

    /// <summary>
    /// 完成：出库业务处理
    /// </summary>
    /// <remarks>
    /// 事务内执行：流水 → UnitloadOp → 重置状态 → 拆盘归档 → ArchiveTask → 统一提交
    /// </remarks>
    private async Task HandleCompleteAsync(TransTask transTask)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var unitload = transTask.Unitload;

            if (unitload == null)
            {
                _logger.LogWarning("[TaskCompletion] 出库完成但 Unitload 为空: TaskCode={TaskCode}", transTask.TaskCode);
                LocationAllocator.ArchiveTask(_db, transTask, null, false);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return;
            }

            // 1. 加载额外 Unitload（含 UnitloadItems 用于流水）
            List<Unitload>? additionalUnitloads = null;
            if (!string.IsNullOrEmpty(transTask.Ext2))
            {
                var additionalIds = transTask.Ext2.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(idStr => int.TryParse(idStr, out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();
                if (additionalIds.Count > 0)
                {
                    additionalUnitloads = await _db.Unitloads
                        .Include(u => u.UnitloadItems)
                        .Where(u => additionalIds.Contains(u.UnitloadId))
                        .ToListAsync();
                }
            }

            // 2. 出库流水（主 Unitload）
            if (unitload.UnitloadItems != null)
            {
                foreach (var item in unitload.UnitloadItems)
                {
                    if (item.MaterialId.HasValue)
                    {
                        var flow = LocationAllocator.CreateFlow(unitload, transTask, transTask.EndLocationId, Cst.出库, item.MaterialId.Value, item);
                        _db.Flows.Add(flow);
                    }
                }
            }

            // 2.1 额外 Unitload 也创建 Flow 和 UnitloadOp 流水
            if (additionalUnitloads != null)
            {
                foreach (var au in additionalUnitloads)
                {
                    if (au.UnitloadItems != null)
                    {
                        foreach (var item in au.UnitloadItems)
                        {
                            if (item.MaterialId.HasValue)
                            {
                                var flow = LocationAllocator.CreateFlow(au, transTask, transTask.EndLocationId, Cst.出库, item.MaterialId.Value, item);
                                _db.Flows.Add(flow);
                            }
                        }
                    }

                    LocationAllocator.AddUnitloadOp(_db, au.ContainerCode ?? string.Empty,
                        UnitloadOps_Enum.OpType.自动.ToString(), UnitloadOps_Enum.Direction.出库.ToString(),
                        $"出库完成 TaskCode={transTask.TaskCode}");
                }
            }

            // 3. UnitloadOp 流水（主 Unitload）
            LocationAllocator.AddUnitloadOp(_db, unitload.ContainerCode ?? string.Empty,
                UnitloadOps_Enum.OpType.自动.ToString(), UnitloadOps_Enum.Direction.出库.ToString(),
                $"出库完成 TaskCode={transTask.TaskCode}");

            // 4. Unitload 状态重置
            unitload.BeingMoved = false;
            unitload.Allocated = false;
            unitload.LocationId = transTask.EndLocationId;
            unitload.CurrentLocationTime = DateTime.Now;

            // 4.1 额外 Unitload 也重置状态
            if (additionalUnitloads != null)
            {
                foreach (var u in additionalUnitloads)
                {
                    u.BeingMoved = false;
                    u.Allocated = false;
                    u.LocationId = transTask.EndLocationId;
                    u.CurrentLocationTime = DateTime.Now;
                }
            }

            // 5. 减少起始位置的出库计数和托盘数
            var startLocation = transTask.StartLocation;
            if (startLocation != null)
            {
                if (startLocation.OutboundCount > 0)
                    startLocation.OutboundCount--;

                // 按实际出库的托盘数减少（1 + 额外托盘数）
                var unitloadCountDelta = 1 + (additionalUnitloads?.Count ?? 0);
                startLocation.UnitloadCount = Math.Max(0, startLocation.UnitloadCount - unitloadCountDelta);
            }

            // 减少终点 InboundCount（起点≠终点时，如定时器创建的出库任务）
            if (transTask.EndLocation != null
                && transTask.StartLocationId != transTask.EndLocationId
                && transTask.EndLocation.InboundCount > 0)
            {
                transTask.EndLocation.InboundCount--;
            }

            // 6. 如果 UnitloadItems 是多个，进行拆盘+归档
            if (unitload.UnitloadItems != null && unitload.UnitloadItems.Count > 1)
            {
                _logger.LogInformation("[TaskCompletion] 拆盘: UnitloadId={UnitloadId}, ContainerCode={ContainerCode}, Items={Count}",
                    unitload.UnitloadId, unitload.ContainerCode, unitload.UnitloadItems.Count);

                LocationAllocator.SplitUnitload(_db, unitload, transTask.EndLocationId);
            }

            // 7. 任务归档
            LocationAllocator.ArchiveTask(_db, transTask, null, false);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
