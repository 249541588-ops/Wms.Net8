using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.Handlers.WcsRequest;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Extensions;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Persistence;
using WcsRequestDto = Wms.Core.Application.DTOs.WcsRequest;

namespace Wms.Core.Infrastructure.Handlers.WcsRequest;

/// <summary>
/// 入库双叉请求处理器
/// </summary>
public class InboundDoubleRequestHandler : IWcsRequestHandler
{
    private readonly WmsDbContext _db;
    private readonly LocationAllocator _allocator;
    private readonly IWcsTaskBridge _wcsBridge;
    private readonly ILogger<InboundDoubleRequestHandler> _logger;

    /// <summary>
    /// 
    /// </summary>
    public string RequestType => Cst.入库双叉;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="db"></param>
    /// <param name="allocator"></param>
    /// <param name="wcsBridge"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public InboundDoubleRequestHandler(
        WmsDbContext db,
        LocationAllocator allocator,
        IWcsTaskBridge wcsBridge,
        ILogger<InboundDoubleRequestHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        _wcsBridge = wcsBridge ?? throw new ArgumentNullException(nameof(wcsBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <param name="location"></param>
    /// <returns></returns>
    public async Task<WcsResult> HandleAsync(WcsRequestDto request, Location location)
    {
        // 确保 location 被 DbContext 跟踪（避免 MemoryCache 缓存的游离实体导致跟踪冲突）
        location = await _db.Locations.FindAsync(location.LocationId) ?? location;

        _logger.LogInformation("[WcsRequest] 入库请求: 位置={Location}, 容器={Container}",
            location.LocationCode, string.Join(",", request.ContainerCode ?? []));

        // 1. 基础参数验证
        if (request.ContainerCode == null || request.ContainerCode.Length == 0)
        {
            return ApiResultHelper.WcsFail("容器编码不能为空", ResultCodeTypes.数据异常, -1);
        }

        if (string.IsNullOrWhiteSpace(location.Tag))
        {
            return ApiResultHelper.WcsFail($"位置 {location.LocationCode} 的 Tag 为空，无法匹配工艺", ResultCodeTypes.数据异常, -1);
        }

        // 2. 起点位置验证
        if (location.InboundDisabled)
        {
            return ApiResultHelper.WcsFail($"位置 {location.LocationCode} 已禁止入库", ResultCodeTypes.数据异常, -1);
        }

        if (location.InboundCount >= location.InboundLimit)
        {
            return ApiResultHelper.WcsFail($"位置 {location.LocationCode} 入库任务数已达上限 ({location.InboundCount}/{location.InboundLimit})",
                ResultCodeTypes.数据异常, -1);
        }

        // 3. 遍历容器码
        Location? firstTarget = null;
        string? firstTargetGroup = null;

        foreach (var containerCode in request.ContainerCode)
        {
            if (string.IsNullOrWhiteSpace(containerCode))
                continue;

            // 3a. 查询 Unitload（先按 ContainerCode 查，查不到再按 UnitloadItem.BoxCode 查）
            var unitload = await _db.Unitloads
                .FirstOrDefaultAsync(u => u.ContainerCode == containerCode);
            if (unitload == null)
            {
                var item = await _db.UnitloadItems
                    .FirstOrDefaultAsync(i => i.BoxCode == containerCode);
                if (item?.UnitloadId != null)
                {
                    unitload = await _db.Unitloads
                        .FirstOrDefaultAsync(u => u.UnitloadId == item.UnitloadId.Value);
                }
            }
            if (unitload == null)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 不存在", ResultCodeTypes.数据异常, -1);
            }

            // 3b. 验证 Unitload 状态
            if (unitload.BeingMoved == true)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 正在移动中", ResultCodeTypes.任务重复, -1);
            }

            if (unitload.Allocated == true)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 已被分配", ResultCodeTypes.任务重复, -1);
            }

            // 托盘当前位置类型是不是 R（货架位）
            if (unitload.LocationId != null)
            {
                var currentLocation = await _db.Locations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.LocationId == unitload.LocationId.Value);
                if (currentLocation != null && currentLocation.LocationType == Location_Enum.LocationType.R.ToString())
                {
                    return ApiResultHelper.WcsFail($"托盘 {containerCode} 当前在货架位 {currentLocation.LocationCode}，无法入库",
                        ResultCodeTypes.数据异常, -1);
                }

                // 托盘当前是否已有任务在执行
                var existingTask = await _db.TransTasks
                    .AnyAsync(t => t.UnitloadId == unitload.UnitloadId
                        && t.ForWcs == true
                        && (t.WasSentToWcs != true));
                if (existingTask)
                {
                    return ApiResultHelper.WcsFail($"托盘 {containerCode} 已有任务在执行", ResultCodeTypes.任务重复, -1);
                }
            }

            // 3c. 验证工艺匹配
            if (!LocationAllocator.IsTagMatch(location.Tag, unitload.NextOperation))
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 工艺 {unitload.NextOperation} 与位置 Tag {location.Tag} 不匹配",
                    ResultCodeTypes.数据异常, -1);
            }

            // 3d. 分配货位（第二个及以后容器优先临近分配）
            Location? targetLocation;
            if (firstTarget != null)
            {
                targetLocation = await _allocator.AllocateNearbyAsync(firstTarget, location, unitload);
            }
            else
            {
                targetLocation = await _allocator.AllocateAsync(location, unitload);
            }
            if (targetLocation == null)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 分配货位失败，无可用库位", ResultCodeTypes.程序异常, -1);
            }

            // 验证起点与终点不能一样
            if (location.LocationId == targetLocation.LocationId)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 起点与终点相同（{location.LocationCode}），无法入库",
                    ResultCodeTypes.数据异常, -1);
            }

            // 记录第一个目标位置，供后续容器临近分配使用
            firstTarget ??= targetLocation;
            firstTargetGroup ??= StringExtensions.GenerateTimeStamp();

            // 3e. 创建 TransTask（在事务中保证 AppSeqs + TransTask + Unitload/Location 原子性）
            await using var tx = await _db.Database.BeginTransactionAsync();

            var transTask = new TransTask
            {
                TaskCode = await TaskCodeGenerator.GenerateAsync(_db),
                TaskType = RequestType,
                UnitloadId = unitload.UnitloadId,
                UnitloadCode = containerCode,
                StartLocationId = location.LocationId,
                EndLocationId = targetLocation.LocationId,
                ForWcs = true,
                WasSentToWcs = false,
                Ext1 = containerCode,
                WareHouse = location.AreaName,
                LocationGroup = firstTargetGroup
            };
            _db.TransTasks.Add(transTask);

            // 设置导航属性，确保 DatabaseWcsTaskBridge 回退时能获取正确的 ContainerCode/LocationCode
            transTask.Unitload = unitload;
            transTask.StartLocation = location;
            transTask.EndLocation = targetLocation;

            // 3f. 更新 Unitload 和位置状态
            unitload.BeingMoved = true;
            unitload.Allocated = true;
            unitload.LocationId = location.LocationId;
            location.OutboundCount++;
            targetLocation.InboundCount++;

            // 先持久化 WMS 状态（TransTask + Unitload + Location），再发送 WCS，保证事务完整性
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // 3g. 下发 WCS
            await _wcsBridge.SendTaskAsync(transTask);
            transTask.WasSentToWcs = true;
            transTask.SentToWcsAt = DateTime.Now;

            _logger.LogInformation("[WcsRequest] 入库任务已创建: TaskCode={TaskCode}, {Container} → {Target}",
                transTask.TaskCode, containerCode, targetLocation.LocationCode);

            await _db.SaveChangesAsync();
        }

        return ApiResultHelper.WcsSuccess("入库请求处理成功", ResultCodeTypes.一, 1);
    }
}
