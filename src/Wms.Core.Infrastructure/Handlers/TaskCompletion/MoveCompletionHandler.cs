using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Application.Ports;
using Wms.Core.Infrastructure.Handlers.WcsRequest;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Handlers.TaskCompletion;

/// <summary>
/// 移库完成处理器
/// </summary>
/// <remarks>
/// WCS 完成移库搬运后的业务处理：
/// 完成时：流水 → UnitloadOp → 重置状态 → Location 计数 → ArchiveTask →（事务外）通知杭可
/// 取消时：回退 Unitload/Location 状态 → ArchiveTask
/// </remarks>
public class MoveCompletionHandler : ITaskCompletionHandler
{
    /// <summary>
    /// 任务类型：移库
    /// </summary>
    public string[] TaskTypes => [Cst.移库];

    private readonly WmsDbContext _db;
    private readonly ILogger<MoveCompletionHandler> _logger;
    private readonly IHangKeClient _hangkeClient;
    private readonly HangKeClientOptions _hangkeOptions;

    /// <summary>
    /// 初始化移库完成处理器
    /// </summary>
    public MoveCompletionHandler(
        WmsDbContext db,
        ILogger<MoveCompletionHandler> logger,
        IHangKeClient hangkeClient,
        IOptions<HangKeClientOptions> hangkeOptions)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hangkeClient = hangkeClient ?? throw new ArgumentNullException(nameof(hangkeClient));
        _hangkeOptions = hangkeOptions?.Value ?? throw new ArgumentNullException(nameof(hangkeOptions));
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
            // 查找 TransTask（Include 导航属性用于流水/归档/杭可通知）
            var transTask = await _db.TransTasks
                .Include(t => t.Unitload).ThenInclude(u => u.UnitloadItems).ThenInclude(ui => ui.Material)
                .Include(t => t.StartLocation).ThenInclude(l => l!.Rack).ThenInclude(r => r!.Laneway)
                .Include(t => t.EndLocation).ThenInclude(l => l!.Rack).ThenInclude(r => r!.Laneway)
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
    /// 事务外执行：通知杭可（终点入库 + 起点出库）
    /// </remarks>
    private async Task HandleCompleteAsync(TransTask transTask)
    {
        // 在事务开始前抓取 containerCode（事务外通知需要）
        var containerCode = transTask.Unitload?.ContainerCode ?? string.Empty;

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
                    if (item.MaterialId.HasValue
                        && item.Material?.MaterialCode != CommonTypes.空托盘
                        && item.Material?.MaterialCode != CommonTypes.工装板)
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

        // ===== 事务提交后：通知杭可（HTTP 调用，不影响主流程）=====
        if (_hangkeOptions.Enable && !string.IsNullOrWhiteSpace(containerCode))
        {
            await NotifyHangkeAsync(transTask, containerCode);
        }
    }

    /// <summary>
    /// 通知杭可移库完成（事务外执行）
    /// </summary>
    /// <remarks>
    /// 业务逻辑（参考旧版移库完成程序）：
    /// - 仅当终点库位在化成/分容柜库区（L1/L4/L5/L6）时通知
    /// - 先通知终点"入库"，再通知起点"出库"（起点不判库区，按旧版行为）
    /// - 入库通知成功后更新终点为"双禁"（待杭可后续出库通知解除）
    /// 失败处理：HTTP 异常或 ResultCode≠1 仅记日志，不抛出，不影响主流程
    /// </remarks>
    private async Task NotifyHangkeAsync(TransTask transTask, string containerCode)
    {
        try
        {
            var startLocation = transTask.StartLocation;
            var endLocation = transTask.EndLocation;

            if (endLocation == null)
            {
                _logger.LogWarning("[TaskCompletion] 移库杭可通知：终点库位为空，跳过: TaskCode={TaskCode}", transTask.TaskCode);
                return;
            }

            var endLanewayCode = endLocation.Rack?.Laneway?.LanewayCode;
            if (string.IsNullOrEmpty(endLanewayCode) || !CommonTypes.化成分容柜对应库区.Contains(endLanewayCode))
            {
                _logger.LogDebug("[TaskCompletion] 移库杭可通知：终点 {LocCode} 不在化成柜库区（Laneway={Laneway}），跳过",
                    endLocation.LocationCode, endLanewayCode ?? "(空)");
                return;
            }

            // 1. 通知杭可已入库（终点）
            // 注意：InOutNotifyAsync 内部对空 Position/TrayCode 会抛异常（自吞为 ResultCode=-1）
            // 此处严格判空，避免无意义的 HTTP 调用 + 让日志更清晰
            var endPosition = endLocation.AnotherCode;
            if (string.IsNullOrWhiteSpace(endPosition))
            {
                _logger.LogWarning("[TaskCompletion] 移库杭可通知：终点 {LocCode} 的 AnotherCode 为空，跳过入库通知: TaskCode={TaskCode}",
                    endLocation.LocationCode, transTask.TaskCode);
            }
            else
            {
                var inResult = await _hangkeClient.InOutNotifyAsync(endPosition, containerCode, InOutType_Enum.入库);

                if (inResult.ResultCode == 1)
                {
                    _logger.LogInformation("[TaskCompletion] 托盘 {ContainerCode} 在 {LocCode}({Laneway}) 已完成入库，杭可入库通知成功: ResultCode={Code}",
                        containerCode, endLocation.LocationCode, endLanewayCode, inResult.ResultCode);
                }
                else
                {
                    _logger.LogWarning("[TaskCompletion] 托盘 {ContainerCode} 在 {LocCode}({Laneway}) 已完成入库，杭可入库通知失败: ResultCode={Code}, 原因={Msg}",
                        containerCode, endLocation.LocationCode, endLanewayCode, inResult.ResultCode, inResult.ResultMessage);
                }

                // 入库通知成功后，更新终点为"双禁"状态（与旧版一致）
                if (inResult.ResultCode == 1)
                {
                    endLocation.InboundDisabled = true;
                    endLocation.OutboundDisabled = true;
                    endLocation.InboundDisabledComment = $"{containerCode} 入库完成，禁入";
                    endLocation.OutboundDisabledComment = $"{containerCode} 入库完成，待杭可通知出库";
                    await _db.SaveChangesAsync();
                }
            }

            // 2. 通知杭可已出库（起点）— 旧版不判起点库区，保持原行为
            if (startLocation != null)
            {
                var startPosition = startLocation.AnotherCode;
                if (string.IsNullOrWhiteSpace(startPosition))
                {
                    _logger.LogWarning("[TaskCompletion] 移库杭可通知：起点 {LocCode} 的 AnotherCode 为空，跳过出库通知: TaskCode={TaskCode}",
                        startLocation.LocationCode, transTask.TaskCode);
                }
                else
                {
                    var startLanewayCode = startLocation.Rack?.Laneway?.LanewayCode ?? "(空)";
                    var outResult = await _hangkeClient.InOutNotifyAsync(startPosition, containerCode, InOutType_Enum.出库);

                    if (outResult.ResultCode == 1)
                    {
                        _logger.LogInformation("[TaskCompletion] 托盘 {ContainerCode} 在 {LocCode}({Laneway}) 已完成出库，杭可出库通知成功: ResultCode={Code}",
                            containerCode, startLocation.LocationCode, startLanewayCode, outResult.ResultCode);
                    }
                    else
                    {
                        _logger.LogWarning("[TaskCompletion] 托盘 {ContainerCode} 在 {LocCode}({Laneway}) 已完成出库，杭可出库通知失败: ResultCode={Code}, 原因={Msg}",
                            containerCode, startLocation.LocationCode, startLanewayCode, outResult.ResultCode, outResult.ResultMessage);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TaskCompletion] 移库杭可通知异常: TaskCode={TaskCode}", transTask.TaskCode);
            // 不 rethrow，不影响主流程
        }
    }
}
