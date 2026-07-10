using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.Handlers.WcsRequest;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Persistence;
using WcsRequestDto = Wms.Core.Application.DTOs.WcsRequest;

namespace Wms.Core.Infrastructure.Handlers.WcsRequest;

/// <summary>
/// 出库请求处理器 — 验证所有容器码，创建一个 TransTask
/// </summary>
public class OutboundRequestHandler : IWcsRequestHandler
{
    private readonly WmsDbContext _db;
    private readonly IWcsTaskBridge _wcsBridge;
    private readonly ILogger<OutboundRequestHandler> _logger;

    public string RequestType => Cst.出库;

    public OutboundRequestHandler(
        WmsDbContext db,
        IWcsTaskBridge wcsBridge,
        ILogger<OutboundRequestHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _wcsBridge = wcsBridge ?? throw new ArgumentNullException(nameof(wcsBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WcsResult> HandleAsync(WcsRequestDto request, Location location)
    {
        // 确保 location 被 DbContext 跟踪（避免 MemoryCache 缓存的游离实体导致跟踪冲突）
        location = await _db.Locations.FindAsync(location.LocationId) ?? location;

        _logger.LogInformation("[WcsRequest] 出库请求: 位置={Location}, 容器={Container}",
            location.LocationCode, string.Join(",", request.ContainerCode ?? []));

        // 1. 基础参数验证
        if (request.ContainerCode == null || request.ContainerCode.Length == 0)
        {
            return ApiResultHelper.WcsFail("容器编码不能为空", ResultCodeTypes.数据异常, -1);
        }

        // 2. 起点位置验证
        if (location.OutboundDisabled)
        {
            return ApiResultHelper.WcsFail($"位置 {location.LocationCode} 已禁止出库", ResultCodeTypes.数据异常, -1);
        }

        if (location.OutboundCount >= location.OutboundLimit)
        {
            return ApiResultHelper.WcsFail($"位置 {location.LocationCode} 出库任务数已达上限 ({location.OutboundCount}/{location.OutboundLimit})",
                ResultCodeTypes.数据异常, -1);
        }

        // 3. 验证所有容器码
        var containerCodes = string.Join(";", request.ContainerCode ?? []);
        var allValidatedUnitloads = new Dictionary<int, Unitload>();

        foreach (var cc in request.ContainerCode ?? [])
        {
            if (string.IsNullOrWhiteSpace(cc)) continue;

            // 3a. 查询 Unitload
            var unitload = await _db.Unitloads
                .FirstOrDefaultAsync(u => u.ContainerCode == cc);
            if (unitload == null)
            {
                return ApiResultHelper.WcsFail($"托盘 {cc} 不存在", ResultCodeTypes.数据异常, -1);
            }

            // 3b. 验证 Unitload 状态
            if (unitload.BeingMoved == true)
            {
                return ApiResultHelper.WcsFail($"托盘 {cc} 正在移动中", ResultCodeTypes.任务重复, -1);
            }

            if (unitload.Allocated == true)
            {
                return ApiResultHelper.WcsFail($"托盘 {cc} 已被分配", ResultCodeTypes.任务重复, -1);
            }

            if (unitload.LocationId != location.LocationId)
            {
                return ApiResultHelper.WcsFail($"托盘 {cc} 不在位置 {location.LocationCode}（当前在位置 {unitload.LocationId}）",
                    ResultCodeTypes.数据异常, -1);
            }

            allValidatedUnitloads.TryAdd(unitload.UnitloadId, unitload);
        }

        // 4. 创建 TransTask（一个任务覆盖所有容器码，出库起点终点相同）
        var firstUnitload = allValidatedUnitloads.Values.First();
        await using var tx = await _db.Database.BeginTransactionAsync();

        var transTask = new TransTask
        {
            TaskCode = await TaskCodeGenerator.GenerateAsync(_db),
            TaskType = Cst.出库,
            UnitloadId = firstUnitload.UnitloadId,
            UnitloadCode = firstUnitload.ContainerCode,
            StartLocationId = location.LocationId,
            EndLocationId = location.LocationId,
            ForWcs = true,
            WasSentToWcs = false,
            Ext1 = containerCodes,
            Ext2 = string.Join(";", allValidatedUnitloads.Keys
                .Where(id => id != firstUnitload.UnitloadId)),
            WareHouse = location.Warehouse?.AreaCode,
            LocationGroup = location.AreaName
        };
        _db.TransTasks.Add(transTask);

        // 设置导航属性，确保 DatabaseWcsTaskBridge 回退时能获取正确的 ContainerCode/LocationCode
        transTask.Unitload = firstUnitload;
        transTask.StartLocation = location;
        transTask.EndLocation = location;

        // 5. 更新所有 Unitload 和位置状态
        foreach (var u in allValidatedUnitloads.Values)
        {
            u.BeingMoved = true;
        }
        location.OutboundCount++;

        // 先持久化 WMS 状态（TransTask + Unitload + Location），再发送 WCS，保证事务完整性
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // 6. 下发 WCS
        await _wcsBridge.SendTaskAsync(transTask);
        transTask.WasSentToWcs = true;
        transTask.SentToWcsAt = DateTime.Now;

        _logger.LogInformation("[WcsRequest] 出库任务已创建: TaskCode={TaskCode}, {Container} ← {Location}",
            transTask.TaskCode, containerCodes, location.LocationCode);

        await _db.SaveChangesAsync();

        return ApiResultHelper.WcsSuccess("出库请求处理成功", ResultCodeTypes.一, 1);
    }
}
