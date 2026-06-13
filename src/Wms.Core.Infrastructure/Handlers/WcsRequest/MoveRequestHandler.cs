using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.Handlers.WcsRequest;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Interfaces;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Persistence;
using WcsRequestDto = Wms.Core.Application.DTOs.WcsRequest;

namespace Wms.Core.Infrastructure.Handlers.WcsRequest;

/// <summary>
/// 移库请求处理器
/// </summary>
public class MoveRequestHandler : IWcsRequestHandler
{
    private readonly WmsDbContext _db;
    private readonly LocationAllocator _allocator;
    private readonly IWcsTaskBridge _wcsBridge;
    private readonly ILogger<MoveRequestHandler> _logger;

    public string RequestType => Cst.移库;

    public MoveRequestHandler(
        WmsDbContext db,
        LocationAllocator allocator,
        IWcsTaskBridge wcsBridge,
        ILogger<MoveRequestHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        _wcsBridge = wcsBridge ?? throw new ArgumentNullException(nameof(wcsBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WcsResult> HandleAsync(WcsRequestDto request, Location location)
    {
        _logger.LogInformation("[WcsRequest] 移库请求: 位置={Location}, 容器={Container}",
            location.LocationCode, string.Join(",", request.ContainerCode ?? []));

        // 1. 基础参数验证
        if (request.ContainerCode == null || request.ContainerCode.Length == 0)
        {
            return ApiResultHelper.WcsFail("容器编码不能为空", ResultCodeTypes.数据异常, -1);
        }

        // 2. 遍历容器码
        foreach (var containerCode in request.ContainerCode)
        {
            if (string.IsNullOrWhiteSpace(containerCode))
                continue;

            // 2a. 查询 Unitload
            var unitload = await _db.Unitloads
                .FirstOrDefaultAsync(u => u.ContainerCode == containerCode);
            if (unitload == null)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 不存在", ResultCodeTypes.数据异常, -1);
            }

            // 2b. 验证 Unitload 状态
            if (unitload.BeingMoved == true)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 正在移动中", ResultCodeTypes.任务重复, -1);
            }

            if (unitload.Allocated == true)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 已被分配", ResultCodeTypes.任务重复, -1);
            }

            if (unitload.LocationId == null)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 无当前位置信息", ResultCodeTypes.数据异常, -1);
            }

            // 2c. 获取 Unitload 当前所在位置
            var startLocation = await _db.Locations
                .FindAsync(unitload.LocationId.Value);
            if (startLocation == null)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 当前位置 {unitload.LocationId} 不存在", ResultCodeTypes.数据异常, -1);
            }

            if (startLocation.OutboundDisabled)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 当前位置 {startLocation.LocationCode} 已禁止出库", ResultCodeTypes.数据异常, -1);
            }

            if (startLocation.OutboundCount >= startLocation.OutboundLimit)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 当前位置 {startLocation.LocationCode} 出库任务数已达上限 ({startLocation.OutboundCount}/{startLocation.OutboundLimit})",
                    ResultCodeTypes.数据异常, -1);
            }

            // 2d. 分配新货位
            var targetLocation = await _allocator.AllocateAsync(startLocation, unitload);
            if (targetLocation == null)
            {
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 分配货位失败，无可用库位", ResultCodeTypes.程序异常, -1);
            }

            // 2e. 创建 TransTask
            await using var tx = await _db.Database.BeginTransactionAsync();

            var transTask = new TransTask
            {
                TaskCode = await TaskCodeGenerator.GenerateAsync(_db),
                TaskType = Cst.移库,
                UnitloadId = unitload.UnitloadId,
                StartLocationId = startLocation.LocationId,
                EndLocationId = targetLocation.LocationId,
                ForWcs = true,
                WasSentToWcs = false,
                Ext1 = containerCode,
                WareHouse = startLocation.Warehouse?.AreaCode,
                LocationGroup = startLocation.AreaName
            };
            _db.TransTasks.Add(transTask);

            // 设置导航属性，确保 DatabaseWcsTaskBridge 回退时能获取正确的 ContainerCode/LocationCode
            transTask.Unitload = unitload;
            transTask.StartLocation = startLocation;
            transTask.EndLocation = targetLocation;

            // 2f. 更新 Unitload 和位置状态
            unitload.BeingMoved = true;
            unitload.Allocated = false;
            startLocation.OutboundCount++;
            targetLocation.InboundCount++;

            // 先持久化 WMS 状态（TransTask + Unitload），再发送 WCS，保证事务完整性
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // 2g. 下发 WCS
            await _wcsBridge.SendTaskAsync(transTask);
            transTask.WasSentToWcs = true;
            transTask.SentToWcsAt = DateTime.Now;

            _logger.LogInformation("[WcsRequest] 移库任务已创建: TaskCode={TaskCode}, {Container}: {Start} → {Target}",
                transTask.TaskCode, containerCode, startLocation.LocationCode, targetLocation.LocationCode);

            await _db.SaveChangesAsync();
        }

        return ApiResultHelper.WcsSuccess("移库请求处理成功", ResultCodeTypes.一, 1);
    }
}
