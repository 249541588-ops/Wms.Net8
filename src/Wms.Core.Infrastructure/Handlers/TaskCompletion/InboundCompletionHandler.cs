using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Enums;
using Wms.Core.Application.Ports;
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
    /// 任务类型：入库 / 入库空托 / 入库双叉
    /// </summary>
    public string[] TaskTypes => [Cst.入库, Cst.入库空托, Cst.入库双叉];

    private readonly WmsDbContext _db;
    private readonly IUnitloadService _unitloadService;
    private readonly IProcessRouteService _processRouteService;
    private readonly IMesClient _mesClient;
    private readonly IHangKeClient _hangkeClient;
    private readonly MesClientOptions _mesOptions;
    private readonly HangKeClientOptions _hangkeOptions;
    private readonly ILogger<InboundCompletionHandler> _logger;

    /// <summary>
    /// 初始化入库完成处理器
    /// </summary>
    public InboundCompletionHandler(
        WmsDbContext db,
        IUnitloadService unitloadService,
        IProcessRouteService processRouteService,
        IMesClient mesClient,
        IHangKeClient hangkeClient,
        IOptions<MesClientOptions> mesOptions,
        IOptions<HangKeClientOptions> hangkeOptions,
        ILogger<InboundCompletionHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _unitloadService = unitloadService ?? throw new ArgumentNullException(nameof(unitloadService));
        _processRouteService = processRouteService ?? throw new ArgumentNullException(nameof(processRouteService));
        _mesClient = mesClient ?? throw new ArgumentNullException(nameof(mesClient));
        _hangkeClient = hangkeClient ?? throw new ArgumentNullException(nameof(hangkeClient));
        _mesOptions = mesOptions?.Value ?? throw new ArgumentNullException(nameof(mesOptions));
        _hangkeOptions = hangkeOptions?.Value ?? throw new ArgumentNullException(nameof(hangkeOptions));
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
                .Include(t => t.Unitload).ThenInclude(u => u.UnitloadItems).ThenInclude(ui => ui.Material)
                .Include(t => t.StartLocation)
                .Include(t => t.EndLocation).ThenInclude(l => l!.Rack).ThenInclude(r => r!.Laneway)
                .FirstOrDefaultAsync(t => t.TaskCode == wcsTask.TaskCode);
            if (transTask == null)
            {
                _logger.LogWarning("[TaskCompletion] 未找到 TransTask: TaskCode={TaskCode}", wcsTask.TaskCode);
                return;
            }

            // 2. 更新 Unitload（已通过 Include 加载，直接使用导航属性）
            var unitload = transTask.Unitload;
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
                        .Include(u => u.UnitloadItems).ThenInclude(ui => ui.Material)
                        .Where(u => additionalIds.Contains(u.UnitloadId))
                        .ToListAsync();
                }
            }

            // ===== 事务内：仅 DB 操作（步骤 1-5）=====
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (isCompleted)
                {
                    // === 入库成功 ===
                    if (unitload != null)
                    {
                        // 工序更新：NextOperation → CurrentOperation，再查询新的 NextOperation
                        if (unitload.ProcessRouteVersionId.HasValue)
                        {
                            await _processRouteService.AdvanceOperationAsync(unitload, null);
                        }
                        else
                        {
                            unitload.CurrentOperation = unitload.NextOperation;
                            unitload.NextOperation = _unitloadService.GetNextOperation(unitload.CurrentOperation);
                        }

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

                // 5. 入库成功时创建流水记录
                if (isCompleted)
                {
                    // 5.1 主 Unitload 流水（跳过空托盘/工装板）
                    if (unitload != null)
                    {
                        var firstItem = unitload.UnitloadItems?.FirstOrDefault();
                        if (firstItem?.MaterialId != null
                            && firstItem.Material?.MaterialCode != CommonTypes.空托盘
                            && firstItem.Material?.MaterialCode != CommonTypes.工装板)
                        {
                            _db.Flows.Add(LocationAllocator.CreateFlow(unitload, transTask, transTask.EndLocationId, Cst.入库, firstItem.MaterialId.Value, firstItem));
                        }

                        LocationAllocator.AddUnitloadOp(_db, unitload.ContainerCode ?? string.Empty,
                            UnitloadOps_Enum.OpType.自动.ToString(), UnitloadOps_Enum.Direction.入库.ToString(),
                            $"入库完成 TaskCode={transTask.TaskCode}");
                    }

                    // 5.2 额外 Unitload 流水（跳过空托盘/工装板）
                    foreach (var au in additionalUnitloads)
                    {
                        var firstItem = au.UnitloadItems?.FirstOrDefault();
                        if (firstItem?.MaterialId != null
                            && firstItem.Material?.MaterialCode != CommonTypes.空托盘
                            && firstItem.Material?.MaterialCode != CommonTypes.工装板)
                        {
                            _db.Flows.Add(LocationAllocator.CreateFlow(au, transTask, transTask.EndLocationId, Cst.入库, firstItem.MaterialId.Value, firstItem));
                        }

                        LocationAllocator.AddUnitloadOp(_db, au.ContainerCode ?? string.Empty,
                            UnitloadOps_Enum.OpType.自动.ToString(), UnitloadOps_Enum.Direction.入库.ToString(),
                            $"入库完成 TaskCode={transTask.TaskCode}");
                    }
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            // ===== 事务提交后：外部调用（步骤 6-7）=====

            // 6. 上传 MES
            if (isCompleted && _mesOptions.Enable)
            {
                try
                {
                    if (targetLocation == null)
                    {
                        _logger.LogWarning("[TaskCompletion] 目标库位为空，跳过 MES 上传: TaskCode={TaskCode}", wcsTask.TaskCode);
                    }
                    else
                    {
                        var codes = new List<string>();
                        if (!string.IsNullOrWhiteSpace(unitload?.ContainerCode))
                            codes.Add(unitload.ContainerCode);
                        codes.AddRange(additionalUnitloads
                            .Select(u => u.ContainerCode)
                            .Where(c => !string.IsNullOrWhiteSpace(c)));

                        if (codes.Count > 0)
                        {
                            var mesResult = await _mesClient.SaveUploadMesInfoAsync(codes.ToArray(), targetLocation, DateTime.Now, 1);
                            _logger.LogInformation("[TaskCompletion] MES 上传结果: TaskCode={TaskCode}, Status={Status}, Message={Msg}",
                                wcsTask.TaskCode, mesResult.status, mesResult.message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TaskCompletion] MES 上传失败: TaskCode={TaskCode}", wcsTask.TaskCode);
                }
            }

            // 7. 通知杭可
            if (isCompleted && _hangkeOptions.Enable)
            {
                try
                {
                    if (targetLocation == null || unitload == null)
                    {
                        _logger.LogWarning("[TaskCompletion] 目标库位或托盘为空，跳过杭可通知: TaskCode={TaskCode}", wcsTask.TaskCode);
                    }
                    else if (string.IsNullOrWhiteSpace(unitload.ContainerCode))
                    {
                        _logger.LogWarning("[TaskCompletion] 托盘条码为空，跳过杭可通知: TaskCode={TaskCode}", wcsTask.TaskCode);
                    }
                    else
                    {
                        var lanewayCode = targetLocation.Rack?.Laneway?.LanewayCode;
                        if (!string.IsNullOrEmpty(lanewayCode) && CommonTypes.化成分容柜对应库区.Contains(lanewayCode))
                        {
                            // 先调用杭可通知（HTTP），成功后再改 DB 禁入状态
                            var result = await _hangkeClient.InOutNotifyAsync(
                                targetLocation.AnotherCode ?? "",
                                unitload.ContainerCode,
                                InOutType_Enum.入库);

                            if (result.ResultCode == 1)
                            {
                                _logger.LogInformation("[TaskCompletion] 托盘 {ContainerCode} 杭可入库通知成功: 库位={LocCode}, ResultCode={Code}",
                                    unitload.ContainerCode, targetLocation.LocationCode, result.ResultCode);
                            }
                            else
                            {
                                _logger.LogWarning("[TaskCompletion] 托盘 {ContainerCode} 杭可入库通知失败: 库位={LocCode}, ResultCode={Code}, 原因={Msg}",
                                    unitload.ContainerCode, targetLocation.LocationCode, result.ResultCode, result.ResultMessage);
                            }

                            // 杭可成功后禁入库位
                            if (result.ResultCode == 1)
                            {
                                targetLocation.InboundDisabled = true;
                                targetLocation.InboundDisabledComment = $"{unitload.ContainerCode} 入库完成,禁入";
                                await _db.SaveChangesAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TaskCompletion] 杭可通知异常: TaskCode={TaskCode}", wcsTask.TaskCode);
                }
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
