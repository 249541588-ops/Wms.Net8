using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Enums;
using Wms.Core.Application.Ports;
using Wms.Core.Infrastructure.Handlers.WcsRequest;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Controllers.Sys;

/// <summary>
/// SIM 工具控制器 — 修改工艺、库位、清理库位、创建货载、拆盘、修改标识
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class SimToolController : ControllerBase
{
    private readonly WmsDbContext _db;
    private readonly IUnitloadService _unitloadService;
    private readonly IBasicDictionaryService _dictService;
    private readonly ILogger<SimToolController> _logger;

    public SimToolController(
        WmsDbContext db,
        IUnitloadService unitloadService,
        IBasicDictionaryService dictService,
        ILogger<SimToolController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _unitloadService = unitloadService ?? throw new ArgumentNullException(nameof(unitloadService));
        _dictService = dictService ?? throw new ArgumentNullException(nameof(dictService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 修改货载工艺信息
    /// </summary>
    [HttpPost("update-operation")]
    public async Task<Result> UpdateOperation([FromBody] SimUpdateOperationRequest request)
    {
        try
        {
            var unitload = await FindByContainerCodeAsync(request.ContainerCode, includeItems: true);
            if (unitload == null)
                return Result.Fail($"托盘 {request.ContainerCode} 不存在");

            if (!string.IsNullOrWhiteSpace(request.CurrentOperation))
            {
                unitload.CurrentOperation = request.CurrentOperation;
                unitload.NextOperation = _unitloadService.GetNextOperation(request.CurrentOperation);
            }
            if (request.OperationNumber.HasValue)
                unitload.OperationNumber = request.OperationNumber.Value;
            if (request.IsAdvance.HasValue)
                unitload.IsAdvance = request.IsAdvance.Value;
            unitload.ModifiedTime = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(request.Batch) && unitload.UnitloadItems != null)
            {
                foreach (var item in unitload.UnitloadItems)
                    item.Batch = request.Batch;
            }

            _unitloadService.AddUnitloadOp(request.ContainerCode,
                UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.其他.ToString(),
                $"SIM修改工艺: {request.CurrentOperation ?? unitload.CurrentOperation}/{request.OperationNumber ?? unitload.OperationNumber}/{request.IsAdvance ?? unitload.IsAdvance}");

            await _db.SaveChangesAsync();
            return Result.Success("修改工艺信息成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SimTool] 修改工艺信息失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 修改货载库位信息（同库位多货载一起搬运）
    /// </summary>
    [HttpPost("update-location")]
    public async Task<Result> UpdateLocation([FromBody] SimUpdateLocationRequest request)
    {
        try
        {
            var unitload = await FindByContainerCodeAsync(request.ContainerCode);
            if (unitload == null)
                return Result.Fail($"托盘 {request.ContainerCode} 不存在");

            if (unitload.Location == null || unitload.Location.LocationCode != request.OldLocationCode)
                return Result.Fail($"托盘 {request.ContainerCode} 当前不在库位 {request.OldLocationCode}");

            var oldLocationId = unitload.Location.LocationId;

            // 验证原库位是否关联未完成的运输任务（完成后任务会归档并删除）
            var oldActiveTasks = await _db.TransTasks.CountAsync(t =>
                t.StartLocationId == oldLocationId || t.EndLocationId == oldLocationId);
            if (oldActiveTasks > 0)
                return Result.Fail($"原库位 {request.OldLocationCode} 关联 {oldActiveTasks} 个未完成任务，不允许操作");

            var newLocation = await _db.Locations.FirstOrDefaultAsync(l => l.LocationCode == request.NewLocationCode);
            if (newLocation == null)
                return Result.Fail($"目标库位 {request.NewLocationCode} 不存在");

            // 验证目标库位是否已有货载
            var existingCount = await _db.Unitloads.CountAsync(u => u.LocationId == newLocation.LocationId);
            if (existingCount > 0)
                return Result.Fail($"目标库位 {request.NewLocationCode} 已有 {existingCount} 个货载，不允许移入");

            // 验证目标库位是否关联未完成的运输任务（完成后任务会归档并删除）
            var activeTasks = await _db.TransTasks.CountAsync(t =>
                t.EndLocationId == newLocation.LocationId);
            if (activeTasks > 0)
                return Result.Fail($"目标库位 {request.NewLocationCode} 关联 {activeTasks} 个未完成任务，不允许移入");

            // 查找同库位所有 Unitload
            var allUnitloads = await _db.Unitloads
                .Where(u => u.LocationId == unitload.LocationId)
                .ToListAsync();

            var now = !string.IsNullOrWhiteSpace(request.CurrentLocationTime) && DateTime.TryParse(request.CurrentLocationTime, out var parsedTime)
                ? parsedTime
                : unitload.CurrentLocationTime ?? DateTime.Now;
            foreach (var u in allUnitloads)
            {
                u.LocationId = newLocation.LocationId;
                u.CurrentLocationTime = now;
            }

            // 更新库位计数
            var oldLocation = unitload.Location;
            var movedCount = allUnitloads.Count;
            oldLocation.UnitloadCount = Math.Max(0, oldLocation.UnitloadCount - movedCount);
            newLocation.UnitloadCount += movedCount;

            _unitloadService.AddUnitloadOp(request.ContainerCode,
                UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.移动.ToString(),
                $"SIM修改库位: {request.OldLocationCode} → {request.NewLocationCode}, 共{movedCount}个");

            await _db.SaveChangesAsync();
            return Result.Success($"修改库位成功，共移动 {movedCount} 个货载");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SimTool] 修改库位信息失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 清理库位信息（清除库位下所有货载的关联）
    /// </summary>
    [HttpPost("clear-location")]
    public async Task<Result> ClearLocation([FromBody] SimClearLocationRequest request)
    {
        try
        {
            var location = await _db.Locations
                .FirstOrDefaultAsync(l => l.LocationCode == request.LocationCode);
            if (location == null)
                return Result.Fail($"库位 {request.LocationCode} 不存在");

            // 查询 Location 的 Cst.None 位置赋值给 Unitload 对象的 LocationId
            var noneLocation = await _db.Locations
                .FirstOrDefaultAsync(l => l.LocationCode == Cst.None);
            if (noneLocation == null)
                return Result.Fail($"默认库位 {Cst.None} 不存在");

            // 验证库位是否关联未完成的运输任务
            var activeTasks = await _db.TransTasks.CountAsync(t =>
                t.StartLocationId == location.LocationId || t.EndLocationId == location.LocationId);
            if (activeTasks > 0)
                return Result.Fail($"库位 {request.LocationCode} 关联 {activeTasks} 个未完成任务，不允许清理");

            var unitloads = await _db.Unitloads
                .Where(u => u.LocationId == location.LocationId)
                .ToListAsync();

            foreach (var u in unitloads)
            {
                u.LocationId = noneLocation.LocationId;
                u.BeingMoved = false;
                u.Allocated = false;
            }

            location.UnitloadCount = 0;

            _unitloadService.AddUnitloadOp(request.LocationCode,
                UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.其他.ToString(),
                $"SIM清理库位: 清除 {unitloads.Count} 个货载关联");

            await _db.SaveChangesAsync();
            return Result.Success($"清理库位成功，共清除 {unitloads.Count} 个货载");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SimTool] 清理库位失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 创建货载信息（参考 UnitloadService.CreateUnitloadManual）
    /// </summary>
    [HttpPost("create-unitload")]
    public async Task<Result> CreateUnitload([FromBody] SimCreateUnitloadRequest request)
    {
        try
        {
            var containerCode = request.ContainerCode;
            if (string.IsNullOrEmpty(containerCode))
                return Result.Fail("托盘码不能为空");

            if (_unitloadService.IsUnitloadExist(containerCode))
                return Result.Fail($"托盘 {containerCode} 已存在");

            if (string.IsNullOrEmpty(request.CurrentOperation))
                return Result.Fail("当前工艺不能为空");

            // 从 PRODUCTIONMATERIAL 字典获取物料配置
            var productionMaterialParents = _dictService.GetItemsByNo("PRODUCTIONMATERIAL");
            var materialDict = productionMaterialParents.FirstOrDefault(x => x.Name == request.CurrentOperation);
            if (materialDict == null)
                return Result.Fail($"未找到工艺 {request.CurrentOperation} 对应的物料配置");

            // 从字典配置获取物料
            var material = await _db.Set<Domain.Entities.Material.Materials>()
                .FirstOrDefaultAsync(m => m.MaterialCode == materialDict.Value);
            if (material == null)
                return Result.Fail($"物料编码 {materialDict.Value} 不存在");

            // 默认位置
            var noneLocation = await _db.Locations.FirstOrDefaultAsync(l => l.LocationCode == Cst.None);
            if (noneLocation == null)
                return Result.Fail("默认库位不存在");

            var now = DateTime.Now;

            // 创建 Unitload
            var unitload = new Unitload
            {
                ContainerCode = containerCode,
                CurrentOperation = request.CurrentOperation,
                Version = 0,
                OperationNumber = request.OperationNumber,
                IsAdvance = request.IsAdvance,
                LocationId = noneLocation.LocationId,
                StorageGroup = Cst.普通,
                OutFlag = string.Empty,
                ContainerSpecification = Cst.普通托盘,
                IsExcludeCurrentUnitload = false,
                IsUpload = false,
                IsToHangke = 0,
                CurrentLocationTime = now,
                CreatedTime = now,
                ModifiedTime = now,
                CreatedBy = "SIM"
            };
            unitload.NextOperation = _unitloadService.GetNextOperation(request.CurrentOperation);
            _db.Unitloads.Add(unitload);
            await _db.SaveChangesAsync();

            // 创建 UnitloadItem
            var unitloadItem = new UnitloadItem
            {
                UnitloadId = unitload.UnitloadId,
                MaterialId = material.MaterialId,
                Quantity = request.BatteryCount,
                Uom = material.Uom,
                OperationNumber = request.OperationNumber,
                BoxCode = containerCode,
                ProductionTime = now,
                IsAdvance = request.IsAdvance
            };
            _db.UnitloadItems.Add(unitloadItem);
            await _db.SaveChangesAsync();

            // 生成随机电芯条码并创建 UnitloadItemDetail
            var barcodes = _unitloadService.GenerateBatteryBarcodes(request.BatteryCount, now.Month, now.Day, 1);

            var batchMap = new Dictionary<string, int>();
            foreach (var kvp in barcodes)
            {
                var detail = new UnitloadItemDetail
                {
                    UnitloadItemId = unitloadItem.UnitloadItemId,
                    LocIndex = kvp.Key,
                    BarCode = kvp.Value,
                    Status = Unitload_Enum.UnitloadItemDetailStatus.正常.ToString()
                };
                _db.UnitloadItemDetails.Add(detail);

                var batch = _unitloadService.GetBatchFromBarcode(kvp.Value);
                if (!string.IsNullOrEmpty(batch))
                {
                    if (batchMap.ContainsKey(batch))
                        batchMap[batch]++;
                    else
                        batchMap[batch] = 1;
                }
            }

            unitloadItem.Batch = batchMap.OrderByDescending(x => x.Value).FirstOrDefault().Key;
            unitloadItem.OutOrdering = unitloadItem.Batch;
            await _db.SaveChangesAsync();

            _unitloadService.AddUnitloadOp(containerCode,
                UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.入库.ToString(),
                "SIM创建货载");

            await _db.SaveChangesAsync();
            return Result.Success("创建货载成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SimTool] 创建货载失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 拆盘功能
    /// </summary>
    [HttpPost("split-unitload")]
    public async Task<Result> SplitUnitload([FromBody] SimSplitUnitloadRequest request)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var unitload = await _db.Unitloads
                .Include(u => u.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                .FirstOrDefaultAsync(u => u.ContainerCode == request.ContainerCode);
            if (unitload == null)
                return Result.Fail($"托盘 {request.ContainerCode} 不存在");

            if (unitload.UnitloadItems == null || unitload.UnitloadItems.Count <= 1)
                return Result.Fail($"托盘 {request.ContainerCode} 只有单物料，无需拆盘");

            var targetLocationId = unitload.LocationId ?? 0;
            await LocationAllocator.SplitUnitloadAsync(_db, unitload, targetLocationId);

            _unitloadService.AddUnitloadOp(request.ContainerCode,
                UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.拆盘.ToString(),
                "SIM拆盘");

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result.Success("拆盘成功");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[SimTool] 拆盘失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 修改货载移动和分配标识
    /// </summary>
    [HttpPost("update-movement-flags")]
    public async Task<Result> UpdateMovementFlags([FromBody] SimUpdateMovementFlagsRequest request)
    {
        try
        {
            var unitload = await FindByContainerCodeAsync(request.ContainerCode);
            if (unitload == null)
                return Result.Fail($"托盘 {request.ContainerCode} 不存在");

            var updated = false;
            if (request.BeingMoved.HasValue)
            {
                unitload.BeingMoved = request.BeingMoved.Value;
                updated = true;
            }
            if (request.Allocated.HasValue)
            {
                unitload.Allocated = request.Allocated.Value;
                updated = true;
            }
            if (!updated)
                return Result.Fail("未指定需要修改的标识");
            unitload.ModifiedTime = DateTime.Now;

            _unitloadService.AddUnitloadOp(request.ContainerCode,
                UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.其他.ToString(),
                $"SIM修改标识: BeingMoved={request.BeingMoved ?? unitload.BeingMoved}, Allocated={request.Allocated ?? unitload.Allocated}");

            await _db.SaveChangesAsync();
            return Result.Success("修改标识成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SimTool] 修改标识失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 叠盘功能
    /// </summary>
    [HttpPost("merge-unitload")]
    public async Task<Result> MergeUnitloads([FromBody] SimMergeUnitloadRequest request)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            if (request.TargetContainerCode == request.SourceContainerCode)
                return Result.Fail("目标托盘和来源托盘不能相同");

            var target = await _db.Unitloads
                .Include(u => u.UnitloadItems)
                .FirstOrDefaultAsync(u => u.ContainerCode == request.TargetContainerCode);
            if (target == null)
                return Result.Fail($"目标托盘 {request.TargetContainerCode} 不存在");

            var source = await _db.Unitloads
                .Include(u => u.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                .Include(u => u.Location)
                .FirstOrDefaultAsync(u => u.ContainerCode == request.SourceContainerCode);
            if (source == null)
                return Result.Fail($"来源托盘 {request.SourceContainerCode} 不存在");

            if (source.UnitloadItems == null || source.UnitloadItems.Count == 0)
                return Result.Fail($"来源托盘 {request.SourceContainerCode} 无物料明细，无需叠盘");

            var targetLocId = target.LocationId ?? 0;
            var sourceLocId = source.LocationId ?? 0;
            var activeTasks = await _db.TransTasks.CountAsync(t =>
                t.StartLocationId == targetLocId || t.EndLocationId == targetLocId ||
                t.StartLocationId == sourceLocId || t.EndLocationId == sourceLocId);
            if (activeTasks > 0)
                return Result.Fail($"关联 {activeTasks} 个未完成任务，不允许叠盘");

            await LocationAllocator.MergeUnitloadsAsync(_db, target, source, "SIM叠盘");

            if (sourceLocId > 0)
            {
                var sourceLocation = await _db.Locations
                    .FirstOrDefaultAsync(l => l.LocationId == sourceLocId);
                if (sourceLocation != null)
                    sourceLocation.UnitloadCount = Math.Max(0, sourceLocation.UnitloadCount - 1);
            }

            _unitloadService.AddUnitloadOp(request.TargetContainerCode,
                UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.叠盘.ToString(),
                $"SIM叠盘: {request.SourceContainerCode} → {request.TargetContainerCode}");

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result.Success("叠盘成功");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[SimTool] 叠盘失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    // ==================== 公共方法 ====================

    private async Task<Unitload?> FindByContainerCodeAsync(string code, bool includeItems = false)
    {
        var query = _db.Unitloads.AsQueryable();
        query = query.Include(u => u.Location);
        if (includeItems)
            query = query.Include(u => u.UnitloadItems);
        return await query.FirstOrDefaultAsync(u => u.ContainerCode == code);
    }
}
