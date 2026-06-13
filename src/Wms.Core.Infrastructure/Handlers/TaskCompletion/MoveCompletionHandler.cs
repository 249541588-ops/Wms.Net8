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
/// 移库完成处理器
/// </summary>
/// <remarks>
/// WCS 完成移库搬运后的业务处理：
/// 完成时：流水 → UnitloadOp → 重置状态 → Location 计数 → ArchiveTask
/// 取消时：回退 Unitload/Location 状态 → ArchiveTask
/// </remarks>
public class MoveCompletionHandler : ITaskCompletionHandler
{
    /// <summary>
    /// 任务类型：移库
    /// </summary>
    public string TaskType => Cst.移库;

    private readonly WmsDbContext _db;
    private readonly ILogger<MoveCompletionHandler> _logger;

    /// <summary>
    /// 初始化移库完成处理器
    /// </summary>
    public MoveCompletionHandler(WmsDbContext db, ILogger<MoveCompletionHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 处理移库任务完成/取消
    /// </summary>
    public async Task HandleAsync(WcsTask wcsTask)
    {
        var isCompleted = wcsTask.WcsState == TaskInfoWcsStates.Completed;

        _logger.LogInformation("[TaskCompletion] 移库{Action}: TaskCode={TaskCode}, 容器={ContCode}, 起始={StartLoc}, 目标={EndLoc}",
            isCompleted ? "完成" : "取消",
            wcsTask.TaskCode, wcsTask.ContCode, wcsTask.StartLoc, wcsTask.ActEndLoc ?? wcsTask.EndLoc);

        try
        {
            // 查找 TransTask（Include 导航属性用于流水/归档）
            var transTask = await _db.TransTasks
                .Include(t => t.Unitload).ThenInclude(u => u.UnitloadItems)
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
                // 完成：移库业务处理（含归档、流水）
                await HandleCompleteAsync(transTask);
            }

            _logger.LogInformation("[TaskCompletion] 移库{Action}处理成功: TaskCode={TaskCode}",
                !isCompleted ? "取消" : "完成", wcsTask.TaskCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TaskCompletion] 移库{Action}处理失败: TaskCode={TaskCode}",
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

        if (transTask.StartLocation != null && transTask.StartLocation.OutboundCount > 0)
        {
            transTask.StartLocation.OutboundCount--;
        }

        // 移库取消时还需回退终点 InboundCount
        if (transTask.EndLocation != null && transTask.EndLocation.InboundCount > 0)
        {
            transTask.EndLocation.InboundCount--;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 完成：移库业务处理
    /// </summary>
    /// <remarks>
    /// 事务内执行：流水 → UnitloadOp → 重置状态 → Location 计数 → ArchiveTask → 统一提交
    /// </remarks>
    private async Task HandleCompleteAsync(TransTask transTask)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var unitload = transTask.Unitload;

            if (unitload == null)
            {
                _logger.LogWarning("[TaskCompletion] 移库完成但 Unitload 为空: TaskCode={TaskCode}", transTask.TaskCode);
                LocationAllocator.ArchiveTask(_db, transTask, null, false);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return;
            }

            // 1. 移库流水
            if (unitload.UnitloadItems != null)
            {
                foreach (var item in unitload.UnitloadItems)
                {
                    if (item.MaterialId.HasValue)
                    {
                        var flow = LocationAllocator.CreateFlow(unitload, transTask, transTask.EndLocationId, Cst.移库, item.MaterialId.Value, item);
                        _db.Flows.Add(flow);
                    }
                }
            }

            // 2. UnitloadOp 流水
            LocationAllocator.AddUnitloadOp(_db, unitload.ContainerCode ?? string.Empty,
                UnitloadOps_Enum.OpType.自动.ToString(), UnitloadOps_Enum.Direction.移动.ToString(),
                $"移库完成 TaskCode={transTask.TaskCode}");

            // 3. Unitload 状态重置（移到终点位置）
            unitload.BeingMoved = false;
            unitload.Allocated = false;
            unitload.LocationId = transTask.EndLocationId;
            unitload.CurrentLocationTime = DateTime.Now;

            // 4. 更新 Location 计数
            var startLocation = transTask.StartLocation;
            var endLocation = transTask.EndLocation;

            if (startLocation != null)
            {
                if (startLocation.OutboundCount > 0)
                    startLocation.OutboundCount--;
                startLocation.UnitloadCount = Math.Max(0, startLocation.UnitloadCount - 1);
            }

            if (endLocation != null)
            {
                if (endLocation.InboundCount > 0)
                    endLocation.InboundCount--;
                endLocation.UnitloadCount++;
            }

            // 5. 任务归档
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
