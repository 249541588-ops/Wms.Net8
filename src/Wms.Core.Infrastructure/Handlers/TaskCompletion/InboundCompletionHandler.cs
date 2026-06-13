using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Interfaces;
using Wms.Core.Domain.Services;
using Wms.Core.Infrastructure.Handlers.WcsRequest;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Handlers.TaskCompletion;

/// <summary>
/// 入库完成处理器
/// </summary>
/// <remarks>
/// WCS 完成入库搬运后的业务处理：
/// Completed → 更新 Unitload/Location → 工序推进 → 归档 → 流水
/// Cancelled/Refused → 回退 Unitload 状态 → 归档
/// </remarks>
public class InboundCompletionHandler : ITaskCompletionHandler
{
    /// <summary>
    /// 任务类型：入库
    /// </summary>
    public string TaskType => Cst.入库;

    private readonly WmsDbContext _db;
    private readonly IUnitloadService _unitloadService;
    private readonly ILogger<InboundCompletionHandler> _logger;

    /// <summary>
    /// 初始化入库完成处理器
    /// </summary>
    public InboundCompletionHandler(WmsDbContext db, IUnitloadService unitloadService, ILogger<InboundCompletionHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _unitloadService = unitloadService ?? throw new ArgumentNullException(nameof(unitloadService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 处理入库任务完成
    /// </summary>
    public async Task HandleAsync(WcsTask wcsTask)
    {
        var isCompleted = wcsTask.WcsState == TaskInfoWcsStates.Completed;
        var stateLabel = isCompleted ? "完成" : $"取消/拒绝({wcsTask.WcsState})";

        _logger.LogInformation("[TaskCompletion] 入库{State}: TaskCode={TaskCode}, 容器={ContCode}, 目标库位={EndLoc}",
            stateLabel, wcsTask.TaskCode, wcsTask.ContCode, wcsTask.ActEndLoc ?? wcsTask.EndLoc);

        try
        {
            // 1. 根据 TaskCode 查找 TransTask（Include 导航属性用于归档）
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

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 2. 更新 Unitload（已通过 Include 加载，直接使用导航属性）
                var unitload = transTask.Unitload;

                // 更新目标 Location 状态
                var targetLocation = transTask.EndLocation;

                // 加载额外的 Unitload（Ext2 存储了除 UnitloadId 外的其他 UnitloadId）
                var additionalUnitloads = new List<Unitload>();
                if (!string.IsNullOrEmpty(transTask.Ext2))
                {
                    var additionalIds = transTask.Ext2.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(idStr => int.TryParse(idStr, out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();
                    if (additionalIds.Count > 0)
                    {
                        additionalUnitloads = await _db.Unitloads
                            .Where(u => additionalIds.Contains(u.UnitloadId))
                            .ToListAsync();
                    }
                }

                if (isCompleted)
                {
                    // === 入库成功 ===
                    if (unitload != null)
                    {
                        // 工序更新：NextOperation → CurrentOperation，再查询新的 NextOperation
                        unitload.CurrentOperation = unitload.NextOperation;
                        unitload.NextOperation = _unitloadService.GetNextOperation(unitload.CurrentOperation);

                        unitload.BeingMoved = false;
                        unitload.Allocated = false;
                        unitload.LocationId = transTask.EndLocationId;
                        _logger.LogInformation("[TaskCompletion] Unitload {ContainerCode} 已到位: LocationId={LocationId}",
                            unitload.ContainerCode, transTask.EndLocationId);
                    }

                    // 额外 Unitload 也更新到位
                    foreach (var u in additionalUnitloads)
                    {
                        u.BeingMoved = false;
                        u.Allocated = false;
                        u.LocationId = transTask.EndLocationId;
                    }

                    // 更新目标 Location 状态
                    if (targetLocation != null)
                    {
                        targetLocation.UnitloadCount += 1 + additionalUnitloads.Count;
                    }
                }
                else
                {
                    // === 入库取消/拒绝 ===
                    if (unitload != null)
                    {
                        // 回退：解除移动状态，位置不变
                        unitload.BeingMoved = false;
                        unitload.Allocated = false;
                        _logger.LogInformation("[TaskCompletion] Unitload {ContainerCode} 入库取消，状态已回退",
                            unitload.ContainerCode);
                    }

                    // 额外 Unitload 也回退状态
                    foreach (var u in additionalUnitloads)
                    {
                        u.BeingMoved = false;
                        u.Allocated = false;
                    }
                }

                // 3. 减少起点位置的出库计数
                var startLocation = transTask.StartLocation;
                if (startLocation != null && startLocation.OutboundCount > 0)
                {
                    startLocation.OutboundCount--;
                }

                // 3.1 减少目标点位置的入库计数
                if (targetLocation != null)
                {
                    if (targetLocation.InboundCount > 0)
                        targetLocation.InboundCount--;
                }

                // 4. 任务归档到 ArchivedTasks，同时删除 TransTask
                LocationAllocator.ArchiveTask(_db, transTask, wcsTask.ActEndLoc ?? wcsTask.EndLoc, !isCompleted);

                // 5. 入库成功时创建流水记录（需要有效的 MaterialId）
                if (isCompleted && unitload != null)
                {
                    var firstItem = unitload.UnitloadItems?.FirstOrDefault();
                    if (firstItem?.MaterialId != null)
                    {
                        var flow = LocationAllocator.CreateFlow(unitload, transTask, transTask.EndLocationId, Cst.入库, firstItem.MaterialId.Value, firstItem);
                        _db.Flows.Add(flow);
                    }
                    else
                    {
                        _logger.LogWarning("[TaskCompletion] Unitload {ContainerCode} 无物料信息，跳过流水记录", unitload.ContainerCode);
                    }

                    // 5.1 添加 UnitloadOps 流水
                    LocationAllocator.AddUnitloadOp(_db, unitload.ContainerCode ?? string.Empty,
                        UnitloadOps_Enum.OpType.自动.ToString(), UnitloadOps_Enum.Direction.入库.ToString(),
                        $"入库完成 TaskCode={transTask.TaskCode}");
                }

                // 6. 上传 mes (预留)

                // 7. 上传设备厂家 (预留)

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            _logger.LogInformation("[TaskCompletion] 入库{State}处理成功: TaskCode={TaskCode}", stateLabel, wcsTask.TaskCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TaskCompletion] 入库{State}处理失败: TaskCode={TaskCode}", stateLabel, wcsTask.TaskCode);
            throw;
        }
    }
}
