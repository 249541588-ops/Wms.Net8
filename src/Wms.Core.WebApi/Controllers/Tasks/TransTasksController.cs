using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Repositories;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Domain.Abstractions;

namespace Wms.Core.WebApi.Controllers.Tasks;


/// <summary>
/// 运输任务管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class TransTasksController : ControllerBase
{
    private readonly IRepository<TransTask, int> _repository;
    private readonly WmsDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICtaskDbService _ctaskDb;
    private readonly IEnumerable<ITaskCompletionHandler> _completionHandlers;
    private readonly IFlowEngine _flowEngine;
    private readonly ILogger<TransTasksController> _logger;

    /// <summary>
    ///
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="db"></param>
    /// <param name="ctaskDb"></param>
    /// <param name="completionHandlers"></param>
    /// <param name="flowEngine"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public TransTasksController(
        IRepository<TransTask, int> repository,
        WmsDbContext db,
        ICtaskDbService ctaskDb,
        IEnumerable<ITaskCompletionHandler> completionHandlers,
        IFlowEngine flowEngine,
        ILogger<TransTasksController> logger,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _ctaskDb = ctaskDb ?? throw new ArgumentNullException(nameof(ctaskDb));
        _completionHandlers = completionHandlers ?? throw new ArgumentNullException(nameof(completionHandlers));
        _flowEngine = flowEngine ?? throw new ArgumentNullException(nameof(flowEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取任务列表
    /// </summary>
    /// <param name="keyword">搜索关键字（搜 TaskCode）</param>
    /// <param name="taskType">任务类型筛选（入库/出库/移库）</param>
    /// <param name="containerCode">托盘码（模糊匹配 Unitload.ContainerCode 与 UnitloadCode 快照）</param>
    /// <param name="startLocationCode">起始库位编码（模糊匹配 StartLocation.LocationCode）</param>
    /// <param name="endLocationCode">目标库位编码（模糊匹配 EndLocation.LocationCode）</param>
    /// <param name="ext1">拓展码1（模糊匹配 Ext1）</param>
    /// <param name="wareHouse">库区（按任务类型区分：入库类匹配目标位置 Warehouse.AreaCode；其他/无类型匹配起点位置 Warehouse.AreaCode，精确匹配）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(
        string? keyword = null,
        string? taskType = null,
        string? containerCode = null,
        string? startLocationCode = null,
        string? endLocationCode = null,
        string? ext1 = null,
        string? wareHouse = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _db.Set<TransTask>()
                .Include(t => t.StartLocation)
                .Include(t => t.EndLocation)
                .Include(t => t.Unitload)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(t => t.TaskCode!.Contains(keyword));
            }

            if (!string.IsNullOrEmpty(taskType))
            {
                query = query.Where(t => t.TaskType == taskType);
            }

            // 托盘码：同时匹配 Unitload 导航属性（实时）与 UnitloadCode 快照字段
            if (!string.IsNullOrEmpty(containerCode))
            {
                query = query.Where(t =>
                    (t.Unitload != null && t.Unitload.ContainerCode != null && t.Unitload.ContainerCode.Contains(containerCode))
                    || (t.UnitloadCode != null && t.UnitloadCode.Contains(containerCode)));
            }

            // 起始库位
            if (!string.IsNullOrEmpty(startLocationCode))
            {
                query = query.Where(t => t.StartLocation != null
                    && t.StartLocation.LocationCode != null
                    && t.StartLocation.LocationCode.Contains(startLocationCode));
            }

            // 目标库位
            if (!string.IsNullOrEmpty(endLocationCode))
            {
                query = query.Where(t => t.EndLocation != null
                    && t.EndLocation.LocationCode != null
                    && t.EndLocation.LocationCode.Contains(endLocationCode));
            }

            // 拓展码1
            if (!string.IsNullOrEmpty(ext1))
            {
                query = query.Where(t => t.Ext1 != null && t.Ext1.Contains(ext1));
            }

            // 库区：按任务类型区分匹配位置
            // 入库类（TaskType 含"入库"）→ 目标位置 EndLocation.Warehouse.UserCode
            // 其他（含 TaskType 为空，默认出库语义）→ 起点位置 StartLocation.Warehouse.UserCode
            if (!string.IsNullOrEmpty(wareHouse))
            {
                query = query.Where(t =>
                    // 入库类：按目标位置库区筛选
                    (t.TaskType != null && t.TaskType.Contains("入库")
                        && t.EndLocation != null
                        && t.EndLocation.Warehouse != null
                        && t.EndLocation.Warehouse.UserCode == wareHouse)
                    ||
                    // 其他（含无类型，默认出库）：按起点位置库区筛选
                    ((t.TaskType == null || !t.TaskType.Contains("入库"))
                        && t.StartLocation != null
                        && t.StartLocation.Warehouse != null
                        && t.StartLocation.Warehouse.UserCode == wareHouse));
            }

            var totalCount = query.Count();

            var lists = query
                .OrderByDescending(t => t.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.TaskCode,
                    t.TaskType,
                    t.UnitloadId,
                    ContainerCode = t.Unitload != null ? t.Unitload.ContainerCode : null,
                    StartLocationCode = t.StartLocation != null ? t.StartLocation.LocationCode : null,
                    EndLocationCode = t.EndLocation != null ? t.EndLocation.LocationCode : null,
                    t.ForWcs,
                    t.WasSentToWcs,
                    t.SentToWcsAt,
                    t.OrderCode,
                    WareHouse = (t.TaskType != null && t.TaskType.Contains("入库"))
                        ? (t.EndLocation != null && t.EndLocation.Warehouse != null
                            ? t.EndLocation.Warehouse.xName : null)
                        : (t.StartLocation != null && t.StartLocation.Warehouse != null
                            ? t.StartLocation.Warehouse.xName : null),
                    t.LocationGroup,
                    t.Comment,
                    t.Ext1,
                    t.Ext2,
                    t.CreatedTime
                })
                .ToList();

            var pagedResponse = new PagedResult<object>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<object>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取任务列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 获取任务详情
    /// </summary>
    /// <param name="id">任务 ID</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result GetById(int id)
    {
        try
        {
            var task = _db.Set<TransTask>()
                .Include(t => t.StartLocation)
                .Include(t => t.EndLocation)
                .Include(t => t.Unitload)
                .FirstOrDefault(t => t.Id == id);

            if (task == null)
            {
                return Result.Fail("任务不存在", "404");
            }

            return Result<TransTask>.Success(task, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取任务详情失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 强制完成任务（更新 WcsTask 状态为 completed，委托 ITaskCompletionHandler 处理业务）
    /// </summary>
    /// <param name="id">任务 ID</param>
    [HttpPost("{id:int}/force-complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<Result> ForceComplete(int id)
    {
        return await ForceFinishAsync(id, TaskInfoWcsStates.Completed);
    }

    /// <summary>
    /// 强制取消任务（更新 WcsTask 状态为 cancelled，委托 ITaskCompletionHandler 处理业务）
    /// </summary>
    /// <param name="id">任务 ID</param>
    [HttpPost("{id:int}/force-cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<Result> ForceCancel(int id)
    {
        return await ForceFinishAsync(id, TaskInfoWcsStates.Cancelled);
    }

    /// <summary>
    /// 强制结束任务通用方法：构造 WcsTask → 分发到 Handler → 更新 ctask WcsState
    /// </summary>
    /// <remarks>
    /// 执行顺序：先 Handler（WmsDb 事务）→ 后 ctask 更新。
    /// 避免 ctask 已完成但 WmsDb 事务失败导致数据不一致。
    /// </remarks>
    private async Task<Result> ForceFinishAsync(int id, string wcsState)
    {
        var stateLabel = wcsState == TaskInfoWcsStates.Completed ? "完成" : "取消";
        var logPrefix = wcsState == TaskInfoWcsStates.Completed ? "ForceComplete" : "ForceCancel";

        try
        {
            // 1. 查找 TransTask
            var transTask = await _db.TransTasks
                .Include(t => t.Unitload).ThenInclude(u => u.UnitloadItems).ThenInclude(ui => ui.Material)
                .Include(t => t.StartLocation)
                .Include(t => t.EndLocation)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transTask == null)
            {
                return Result.Fail("任务不存在", "404");
            }

            // ★ T315 状态机校验：已终态任务不允许再次强制完成/取消，避免重复扣库存等业务副作用
            // 终态定义参考 TaskInfoWcsStates.Finished (Completed/Cancelled/Refused) 与 TaskInfoWmsStates.Archived
            if (transTask.WasSentToWcs == true && !string.IsNullOrEmpty(transTask.TaskCode))
            {
                var currentCtask = await _ctaskDb.ReadByTaskCodeAsync(transTask.TaskCode);
                if (currentCtask != null && !string.IsNullOrEmpty(currentCtask.TaskCode))
                {
                    // WMS 端已归档视为终态（WcsTaskSyncService 同样跳过归档任务）
                    if (string.Equals(currentCtask.WmsState, TaskInfoWmsStates.Archived, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("[{Prefix}] 拒绝执行：任务已归档（终态） TaskCode={TaskCode} WmsState={WmsState}",
                            logPrefix, transTask.TaskCode, currentCtask.WmsState);
                        return Result.Fail($"任务已归档，不允许再次强制{stateLabel}", "409");
                    }
                    // WCS 端 Completed/Cancelled/Refused 视为终态
                    if (!string.IsNullOrEmpty(currentCtask.WcsState) &&
                        TaskInfoWcsStates.Finished.Any(s => string.Equals(s, currentCtask.WcsState, StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogWarning("[{Prefix}] 拒绝执行：任务已终态 TaskCode={TaskCode} WcsState={WcsState}",
                            logPrefix, transTask.TaskCode, currentCtask.WcsState);
                        return Result.Fail($"任务已终态（WcsState={currentCtask.WcsState}），不允许再次强制{stateLabel}", "409");
                    }
                }
            }

            _logger.LogInformation("[{Prefix}] 强制{State}任务: TaskCode={TaskCode}, Type={Type}",
                logPrefix, stateLabel, transTask.TaskCode, transTask.TaskType);

            var now = DateTime.Now;

            // 2. 构造 WcsTask
            WcsTask wcsTask;
            bool hasCtaskRecord = false;

            if (transTask.WasSentToWcs == true)
            {
                // 2a. 已发送 WCS：尝试读取 ctask 中的 WcsTask 记录
                wcsTask = await _ctaskDb.ReadByTaskCodeAsync(transTask.TaskCode!);
                if (wcsTask == null || string.IsNullOrEmpty(wcsTask.TaskCode))
                {
                    // ctask 中无记录 或 Dapper 列名映射失败（TaskCode 为空）
                    _logger.LogWarning("[{Prefix}] ctask WcsTask 数据无效: TaskCode={TaskCode}，使用合成数据",
                        logPrefix, wcsTask?.TaskCode ?? "(null)");
                    wcsTask = BuildSyntheticWcsTask(transTask);
                }
                else
                {
                    hasCtaskRecord = true;
                }
            }
            else
            {
                // 2b. 未发送 WCS：直接构造合成 WcsTask
                wcsTask = BuildSyntheticWcsTask(transTask);
            }

            // 3. 设置目标状态
            wcsTask.WcsState = wcsState;
            wcsTask.CompletedAt = now;
            wcsTask.UpdatedAt = now;

            // 4. 先执行业务处理（WmsDb 事务），成功后再更新 ctask
            // 优先：流程引擎（完成阶段）
            var template = await _flowEngine.MatchTemplateAsync(
                transTask.TaskType ?? "", Cst.PhaseCompletion);
            if (template != null)
            {
                var flowContext = new FlowContext(_db, _unitOfWork)
                {
                    Phase = Cst.PhaseCompletion,
                    Unitload = transTask.Unitload,
                    StartLocation = transTask.StartLocation,
                    TargetLocation = transTask.EndLocation,
                    WcsTask = wcsTask,
                    TransTask = transTask,
                    IsCancelled = wcsTask.WcsState != TaskInfoWcsStates.Completed,
                    BusinessType = "TaskCompletion",
                    BusinessId = transTask.TaskCode
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
                            .Include(u => u.UnitloadItems).ThenInclude(ui => ui.Material)
                            .Where(u => additionalIds.Contains(u.UnitloadId))
                            .ToListAsync();
                        if (additionalUnitloads.Count > 0)
                            flowContext.Data["AdditionalUnitloads"] = additionalUnitloads;
                    }
                }

                await _flowEngine.ExecuteCompletionAsync(template, flowContext);
            }
            else
            {
                // 后备：硬编码 Handler
                var handler = _completionHandlers.FirstOrDefault(h => h.TaskTypes.Contains(transTask.TaskType));
                if (handler == null)
                {
                    _logger.LogWarning("[{Prefix}] 未找到任务类型处理器: TaskType={TaskType}", logPrefix, transTask.TaskType);
                    return Result.Fail($"未找到 {transTask.TaskType} 类型的任务处理器");
                }
                await handler.HandleAsync(wcsTask);
            }

            // 5. WmsDb 事务成功后，再更新 ctask 状态
            if (hasCtaskRecord)
            {
                await _ctaskDb.UpdateWcsStateAsync(transTask.TaskCode!, wcsState, now);
                // 标记 wms_state 为 archived，防止轮询重复处理
                await _ctaskDb.UpdateWmsStateAsync(transTask.TaskCode!, TaskInfoWmsStates.Archived);
            }

            _logger.LogInformation("[{Prefix}] 强制{State}处理成功: TaskCode={TaskCode}", logPrefix, stateLabel, transTask.TaskCode);
            return Result.Success($"任务已强制{stateLabel}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Prefix}] 强制{State}任务失败: {Message}", logPrefix, stateLabel, ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 从 TransTask 导航属性构造合成 WcsTask（用于未发送 WCS 或 ctask 记录丢失的场景）
    /// </summary>
    private static WcsTask BuildSyntheticWcsTask(TransTask transTask)
    {
        return new WcsTask
        {
            TaskCode = transTask.TaskCode ?? string.Empty,
            TaskType = transTask.TaskType ?? string.Empty,
            ContCode = transTask.Unitload?.ContainerCode ?? string.Empty,
            ContType = transTask.Unitload?.ContainerSpecification ?? string.Empty,
            StartLoc = transTask.StartLocation?.LocationCode ?? string.Empty,
            EndLoc = transTask.EndLocation?.LocationCode ?? string.Empty,
            Warehouse = transTask.WareHouse,
            LocationGroup = transTask.LocationGroup,
            WmsNote = transTask.Comment
        };
    }

    /// <summary>
    /// 取消未发送的任务
    /// </summary>
    /// <param name="id">任务 ID</param>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<Result> Delete(int id)
    {
        try
        {
            var task = await _db.TransTasks
                .Include(t => t.Unitload)
                .Include(t => t.StartLocation)
                .Include(t => t.EndLocation)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (task == null)
            {
                return Result.Fail("任务不存在", "404");
            }

            if (task.WasSentToWcs == true)
            {
                return Result.Fail("任务已发送至 WCS，无法取消");
            }

            // 回退 Unitload 状态
            if (task.Unitload != null)
            {
                task.Unitload.BeingMoved = false;
                task.Unitload.Allocated = false;
            }

            // 回退额外 Unitload 状态（Ext2 存储了除 UnitloadId 外的其他 UnitloadId）
            if (!string.IsNullOrEmpty(task.Ext2))
            {
                var additionalIds = task.Ext2.Split(';', StringSplitOptions.RemoveEmptyEntries)
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

            // 回退 Location 计数（按任务类型区分）
            var isInbound = task.TaskType == Cst.入库 || task.TaskType == Cst.入库双叉;
            if (isInbound)
            {
                // 入库：起点 OutboundCount--，终点 InboundCount--
                if (task.StartLocation != null && task.StartLocation.OutboundCount > 0)
                    task.StartLocation.OutboundCount--;
                if (task.EndLocation != null && task.EndLocation.InboundCount > 0)
                    task.EndLocation.InboundCount--;
            }
            else
            {
                // 出库/移库：起点 OutboundCount--
                if (task.StartLocation != null && task.StartLocation.OutboundCount > 0)
                    task.StartLocation.OutboundCount--;

                // 出库/移库：回退终点 InboundCount（起点≠终点时）
                if (task.EndLocation != null
                    && task.StartLocationId != task.EndLocationId
                    && task.EndLocation.InboundCount > 0)
                {
                    task.EndLocation.InboundCount--;
                }
            }

            _db.Set<TransTask>().Remove(task);
            await _db.SaveChangesAsync();

            return Result.Success("任务已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消任务失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }
}
