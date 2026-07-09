using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.Handlers.WcsRequest;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Persistence;
using WcsRequestDto = Wms.Core.Application.DTOs.WcsRequest;

namespace Wms.Core.Infrastructure.Handlers.WcsRequest;

/// <summary>
/// 入库请求处理器
/// </summary>
public class InboundEmptyRequestHandler : IWcsRequestHandler
{
    private readonly WmsDbContext _db;
    private readonly LocationAllocator _allocator;
    private readonly IWcsTaskBridge _wcsBridge;
    private readonly ILogger<InboundEmptyRequestHandler> _logger;

    /// <summary>
    ///
    /// </summary>
    public string RequestType => Cst.入库空托;

    /// <summary>
    ///
    /// </summary>
    /// <param name="db"></param>
    /// <param name="allocator"></param>
    /// <param name="wcsBridge"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public InboundEmptyRequestHandler(
        WmsDbContext db,
        LocationAllocator allocator,
        IWcsTaskBridge wcsBridge,
        ILogger<InboundEmptyRequestHandler> logger)
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

        // 3. 处理容器码（3a-3c 验证所有容器码，3d+ 只处理第一个）
        var containerCodes = string.Join(";", request.ContainerCode ?? []);
        var containerCode = request.ContainerCode?.FirstOrDefault(cc => !string.IsNullOrWhiteSpace(cc));
        if (string.IsNullOrWhiteSpace(containerCode))
        {
            return ApiResultHelper.WcsFail("容器编码不能为空", ResultCodeTypes.数据异常, -1);
        }

        // 3a-3c: 验证所有容器码，收集所有验证通过的 Unitload
        var allValidatedUnitloads = new Dictionary<int, Unitload>();
        Unitload? firstValidUnitload = null;

        foreach (var cc in request.ContainerCode ?? [])
        {
            if (string.IsNullOrWhiteSpace(cc)) continue;


            // 3.  查询 Unitload（先按 ContainerCode 查，查不到再按 UnitloadItem.BoxCode 查）
            // 3a. 如果 Unitload 不存在，创建一个物料编码是 CommonTypes.空托盘物料，UnitloadItems 一条记录， UnitloadItemDetail 为空
            // 3b. 如果 Unitload 存在，验证物料是不是 CommonTypes.空托盘物料 ，如果不是返回错误，如果是空物料验证其状态
            var ccUnitload = await _db.Unitloads
                .FirstOrDefaultAsync(u => u.ContainerCode == cc);
            if (ccUnitload == null)
            {
                var item = await _db.UnitloadItems
                    .FirstOrDefaultAsync(i => i.BoxCode == cc);
                if (item?.UnitloadId != null)
                {
                    ccUnitload = await _db.Unitloads
                        .FirstOrDefaultAsync(u => u.UnitloadId == item.UnitloadId.Value);
                }
            }
            if (ccUnitload == null)
            {
                // 3a. Unitload 不存在，创建空托盘
                var emptyMaterial = await _db.Materials
                    .FirstOrDefaultAsync(m => m.MaterialCode == CommonTypes.空托盘);
                var now = DateTime.Now;
                ccUnitload = new Unitload
                {
                    ContainerCode = cc,
                    Version = 0,
                    OperationNumber = 1,
                    IsAdvance = 0,
                    LocationId = location.LocationId,
                    StorageGroup = Cst.普通,
                    OutFlag = string.Empty,
                    ContainerSpecification = Cst.普通托盘,
                    IsExcludeCurrentUnitload = false,
                    IsUpload = false,
                    IsToHangke = 0,
                    CurrentLocationTime = now,
                    CreatedTime = now,
                    ModifiedTime = now,
                    CreatedBy = "WCS",
                };
                ccUnitload.UnitloadItems = new List<UnitloadItem>
                {
                    new()
                    {
                        MaterialId = emptyMaterial?.MaterialId,
                        Quantity = 0,
                        FalseQuantity = 0,
                        Uom = emptyMaterial?.Uom,
                        StockStatus = Cst.合格,
                        BoxCode = cc,
                        ProductionTime = now,
                        OperationNumber = 1,
                        IsAdvance = 0,
                        xLevel = string.Empty,
                    }
                };
                _db.Unitloads.Add(ccUnitload);
                await _db.SaveChangesAsync();
            }

            // 3b. 验证 Unitload 状态
            if (ccUnitload.BeingMoved == true)
            {
                return ApiResultHelper.WcsFail($"托盘 {cc} 正在移动中", ResultCodeTypes.任务重复, -1);
            }

            if (ccUnitload.Allocated == true)
            {
                return ApiResultHelper.WcsFail($"托盘 {cc} 已被分配", ResultCodeTypes.任务重复, -1);
            }

            // 托盘当前位置类型是不是 R（货架位）
            if (ccUnitload.LocationId != null)
            {
                var currentLocation = await _db.Locations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.LocationId == ccUnitload.LocationId.Value);
                if (currentLocation != null && currentLocation.LocationType == Location_Enum.LocationType.R.ToString())
                {
                    return ApiResultHelper.WcsFail($"托盘 {cc} 当前在货架位 {currentLocation.LocationCode}，无法入库",
                        ResultCodeTypes.数据异常, -1);
                }

                // 托盘当前是否已有任务在执行
                var existingTask = await _db.TransTasks
                    .AnyAsync(t => t.UnitloadId == ccUnitload.UnitloadId
                        && t.ForWcs == true
                        && (t.WasSentToWcs != true));
                if (existingTask)
                {
                    return ApiResultHelper.WcsFail($"托盘 {cc} 已有任务在执行", ResultCodeTypes.任务重复, -1);
                }
            }
            
            allValidatedUnitloads.TryAdd(ccUnitload.UnitloadId, ccUnitload);
            firstValidUnitload ??= ccUnitload;
        }

        // 3d 以下使用第一个有效容器码的 unitload
        var unitload = firstValidUnitload!;

        // 3d. 分配货位
        var targetLocation = await _allocator.AllocateAsync(location, unitload);
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

        // 3e. 创建 TransTask（在事务中保证 AppSeqs + TransTask + Unitload/Location 原子性）
        await using var tx = await _db.Database.BeginTransactionAsync();

        var transTask = new TransTask
        {
            TaskCode = await TaskCodeGenerator.GenerateAsync(_db),
            TaskType = RequestType,
            UnitloadId = unitload.UnitloadId,
            UnitloadCode = unitload.ContainerCode,
            StartLocationId = location.LocationId,
            EndLocationId = targetLocation.LocationId,
            ForWcs = true,
            WasSentToWcs = false,
            Ext1 = containerCodes,
            Ext2 = string.Join(";", allValidatedUnitloads.Keys
                .Where(id => id != unitload.UnitloadId)),
            WareHouse = location.AreaName,
            LocationGroup = string.Empty
        };
        _db.TransTasks.Add(transTask);

        // 设置导航属性，确保 DatabaseWcsTaskBridge 回退时能获取正确的 ContainerCode/LocationCode
        transTask.Unitload = unitload;
        transTask.StartLocation = location;
        transTask.EndLocation = targetLocation;

        // 3f. 更新所有 Unitload 和位置状态
        foreach (var u in allValidatedUnitloads.Values)
        {
            u.BeingMoved = true;
            u.Allocated = true;
            u.LocationId = location.LocationId;
            u.CurrentLocationTime = DateTime.Now;
        }
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

        return ApiResultHelper.WcsSuccess("入库请求处理成功", ResultCodeTypes.一, 1);
    }
}
