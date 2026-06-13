using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Interfaces;
using Wms.Core.Domain.Services;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Handlers.WcsRequest;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 出库定时器服务 — 负责定时检查满足条件的托盘并创建出库任务下发给 WCS
/// </summary>
public class OutboundTimerService : IOutboundTimerService
{
    private readonly WmsDbContext _db;
    private readonly IBasicDictionaryService _dictService;
    private readonly IWcsTaskBridge _wcsBridge;
    private readonly ILogger<OutboundTimerService> _logger;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="db"></param>
    /// <param name="dictService"></param>
    /// <param name="wcsBridge"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public OutboundTimerService(
        WmsDbContext db,
        IBasicDictionaryService dictService,
        IWcsTaskBridge wcsBridge,
        ILogger<OutboundTimerService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _dictService = dictService ?? throw new ArgumentNullException(nameof(dictService));
        _wcsBridge = wcsBridge ?? throw new ArgumentNullException(nameof(wcsBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行高温浸润出库任务
    /// </summary>
    public async Task<WcsResult> ExecuteGaowenOutboundAsync()
    {
        try
        {
            // 1a. 获取出库口 LocationCode 列表
            var outboundPorts = _dictService.GetItemsByNo("OUTBOUNDGAOWEN");
            if (outboundPorts == null || outboundPorts.Count == 0)
                return ApiResultHelper.WcsFail("未配置出库口 OUTBOUNDGAOWEN", ResultCodeTypes.数据异常, -1);

            // 1b. 获取浸润时间（分钟）
            var processTime = _dictService.GetByNo("PROCESSTIME_1");
            if (processTime == null || !int.TryParse(processTime.Value, out int minutes))
                return ApiResultHelper.WcsFail("未配置浸润时间 PROCESSTIME_1", ResultCodeTypes.数据异常, -1);
            var cutoffTime = DateTime.Now.AddMinutes(-minutes);

            _logger.LogInformation("[Gaowen] 开始高温浸润出库: 出库口={Count}个, 浸润时间={Minutes}分钟",
                outboundPorts.Count, minutes);

            // 2. 遍历每个出库口
            foreach (var portItem in outboundPorts)
            {
                var locationCode = portItem.Value?.Trim();
                if (string.IsNullOrEmpty(locationCode)) continue;

                try
                {
                    await ProcessOutboundPortAsync(locationCode, cutoffTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Gaowen] 出库口处理失败: {LocationCode}", locationCode);
                }
            }

            return ApiResultHelper.WcsSuccess("高温浸润出库完成", ResultCodeTypes.一, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Gaowen] 高温浸润出库异常");
            return ApiResultHelper.WcsFail($"高温浸润出库异常: {ex.Message}", ResultCodeTypes.程序异常, -1);
        }
    }

    /// <summary>
    /// 处理单个出库口
    /// </summary>
    private async Task ProcessOutboundPortAsync(string locationCode, DateTime cutoffTime)
    {
        _logger.LogInformation("[Gaowen] 处理出库口: {LocationCode}", locationCode);

        // 2a. 获取出库口 Location（AsNoTracking 只读）
        var portLocation = await _db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.LocationCode == locationCode);
        if (portLocation == null)
        {
            _logger.LogWarning("[Gaowen] 出库口不存在: {LocationCode}", locationCode);
            return;
        }
        if (portLocation.OutboundDisabled || portLocation.OutboundCount >= portLocation.OutboundLimit)
        {
            _logger.LogWarning("[Gaowen] 出库口不可用: {LocationCode}, Disabled={Disabled}, Count={Count}/{Limit}",
                locationCode, portLocation.OutboundDisabled, portLocation.OutboundCount, portLocation.OutboundLimit);
            return;
        }

        // 2b. LocationId → Port 列表
        var portIds = await _db.Ports.AsNoTracking()
            .Where(p => p.KP1 == portLocation.LocationId || p.KP2 == portLocation.LocationId)
            .Select(p => p.Id)
            .ToListAsync();
        if (portIds.Count == 0)
        {
            _logger.LogWarning("[Gaowen] 未找到关联 Port: LocationId={LocationId}", portLocation.LocationId);
            return;
        }

        // 2c. Port → LanewayId 列表
        var lanewayIds = await _db.LanewayPorts.AsNoTracking()
            .Where(lp => portIds.Contains(lp.PortId))
            .Select(lp => lp.LanewayId)
            .Distinct()
            .ToListAsync();

        // 2d. Laneway 列表（排除离线和非自动化）
        var laneways = await _db.Laneways.AsNoTracking()
            .Where(l => lanewayIds.Contains(l.LanewayId) && l.Offline != true && l.Automated == true)
            .ToListAsync();

        _logger.LogInformation("[Gaowen] 出库口 {LocationCode}: Port={PortCount}个, Laneway={LanewayCount}个",
            locationCode, portIds.Count, laneways.Count);

        // 3. 遍历每个 Laneway
        foreach (var laneway in laneways)
        {
            try
            {
                await ProcessLanewayAsync(laneway, cutoffTime, portLocation.LocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Gaowen] 巷道处理失败: LanewayId={LanewayId}", laneway.LanewayId);
            }
        }
    }

    /// <summary>
    /// 处理单个巷道的出库批次
    /// </summary>
    private async Task ProcessLanewayAsync(Domain.Entities.Warehouse.Laneway laneway, DateTime cutoffTime, int endLocationId)
    {
        // 3a. 预过滤 OutboundBatch（DB 服务端过滤）
        var batches = await _db.OutboundBatches.AsNoTracking()
            .Where(ob => ob.LanewayId == laneway.LanewayId
                && ob.Status == 1
                && ob.ErrorCount <= 3
                && ob.QuantityDelivered < ob.QuantityRequired)
            .ToListAsync();

        _logger.LogInformation("[Gaowen] 巷道 LanewayId={LanewayId}: 符合条件 OutboundBatches={BatchCount}个",
            laneway.LanewayId, batches.Count);

        foreach (var ob in batches)
        {
            try
            {
                await ProcessBatchAsync(ob, cutoffTime, endLocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Gaowen] Batch 处理异常: Id={Id}, Batch={Batch}", ob.Id, ob.Batch);
            }
        }
    }

    /// <summary>
    /// 处理单个出库批次
    /// </summary>
    private async Task ProcessBatchAsync(Domain.Entities.Outbound.OutboundBatch ob, DateTime cutoffTime, int endLocationId)
    {
        int remaining = ob.QuantityRequired - ob.QuantityDelivered;

        // 3b. UnitloadItem 精确匹配（Batch + MaterialId + OperationNumber + IsAdvance + IsSupplement）
        var query = _db.UnitloadItems.AsNoTracking()
            .Where(ui => ui.MaterialId == ob.MaterialId);

        if (!string.IsNullOrWhiteSpace(ob.Batch))
            query = query.Where(ui => ui.Batch == ob.Batch);
        if (!string.IsNullOrWhiteSpace(ob.xLevel))
            query = query.Where(ui => ui.xLevel == ob.xLevel);
        if (ob.OperationNumber.HasValue)
            query = query.Where(ui => ui.OperationNumber == ob.OperationNumber);
        if (ob.IsAdvance != 0)
            query = query.Where(ui => ui.IsAdvance == ob.IsAdvance);
        if (ob.IsSupplement != 0)
            query = query.Where(ui => ui.IsSupplement == ob.IsSupplement);
       
        var unitloadIds = await query
            .Select(ui => ui.UnitloadId)
            .Distinct()
            .ToListAsync();

        if (unitloadIds.Count == 0)
        {
            _logger.LogWarning("[Gaowen] Batch Id={Id} 未匹配到 UnitloadItem: Batch={Batch}, MaterialId={MaterialId}",
                ob.Id, ob.Batch, ob.MaterialId);
            await HandleBatchNoMatchAsync(ob);
            return;
        }

        // 3c. 筛选 Unitload（CurrentOperation + 时间 + 状态 + 数量上限）
        var unitloads = await _db.Unitloads.AsNoTracking()
            .Where(u => unitloadIds.Contains(u.UnitloadId)
                && u.Location.Rack.LanewayId == ob.LanewayId
                && u.Location.LocationType == Location_Enum.LocationType.R.ToString()
                && u.Location.xExists == true
                && u.Location.OutboundDisabled == false
                && u.CurrentLocationTime < cutoffTime
                && u.BeingMoved != true
                && u.Allocated != true
                && u.LocationId.HasValue)
            .Include(u => u.Location)
            .OrderBy(u => u.CurrentLocationTime)
            .Take(remaining)
            .ToListAsync();

        if (unitloads.Count == 0)
        {
            _logger.LogWarning("[Gaowen] Batch Id={Id} 有 UnitloadItem={ItemCount}个但无满足条件的 Unitload（浸润时间未到/正在移动/已分配）, cutoff={Cutoff}",
                ob.Id, unitloadIds.Count, cutoffTime.ToString("yyyy-MM-dd HH:mm:ss"));
            await HandleBatchNoMatchAsync(ob);
            return;
        }

        _logger.LogInformation("[Gaowen] Batch Id={Id}, Batch={Batch}: 匹配 Unitload={Count}个（剩余需求 {Remaining}）",
            ob.Id, ob.Batch, unitloads.Count, remaining);

        // 3e. 按 LocationId 分组，每个库位创建一个 TransTask（同库位多托盘一起搬运）
        var grouped = unitloads.GroupBy(u => u.LocationId!.Value);
        int successCount = 0;
        foreach (var group in grouped)
        {
            try
            {
                var matchedInGroup = group.ToList();
                if (await ProcessLocationGroupAsync(matchedInGroup, ob.Batch, endLocationId))
                    successCount += matchedInGroup.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Gaowen] Location Group 出库失败: LocationId={LocationId}", group.Key);
            }
        }

        // 3f. 更新 OutboundBatch
        var trackedBatch = await _db.OutboundBatches.FindAsync(ob.Id);
        if (trackedBatch != null)
        {
            if (successCount > 0)
            {
                trackedBatch.QuantityDelivered += successCount;
                trackedBatch.ErrorCount = 0;
                _logger.LogInformation("[Gaowen] Batch 出库成功: Id={Id}, Batch={Batch}, 本次={Success}, 累计={Total}",
                    ob.Id, ob.Batch, successCount, trackedBatch.QuantityDelivered);
            }
            else
            {
                trackedBatch.ErrorCount++;
                if (trackedBatch.ErrorCount > 3)
                    trackedBatch.Status = 0;
            }
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 处理无匹配 Unitload 的情况 — 递增 ErrorCount
    /// </summary>
    private async Task HandleBatchNoMatchAsync(Domain.Entities.Outbound.OutboundBatch ob)
    {
        var tb = await _db.OutboundBatches.FindAsync(ob.Id);
        if (tb != null)
        {
            tb.ErrorCount++;
            if (tb.ErrorCount > 3)
                tb.Status = 0;
            await _db.SaveChangesAsync();
            _logger.LogWarning("[Gaowen] Batch 无满足条件的 Unitload: Id={Id}, Batch={Batch}, ErrorCount={ErrorCount}",
                ob.Id, ob.Batch, tb.ErrorCount);
        }
    }

    /// <summary>
    /// 处理同库位的一组 Unitload 出库（独立事务，支持多托盘）
    /// </summary>
    /// <remarks>
    /// 同库位多托盘一起搬运：第一个匹配的 Unitload 为主，其余同库位 Unitload 存入 Ext1/Ext2。
    /// Ext1 = 所有容器码（;分隔），Ext2 = 额外 UnitloadId（;分隔）
    /// </remarks>
    private async Task<bool> ProcessLocationGroupAsync(List<Unitload> matchedUnitloads, string? batch, int endLocationId)
    {
        var primaryUnitload = matchedUnitloads[0];
        var locationId = primaryUnitload.LocationId!.Value;

        await using var tx = await _db.Database.BeginTransactionAsync();

        // 加载主 Unitload（tracked）
        var trackedPrimary = await _db.Unitloads.FindAsync(primaryUnitload.UnitloadId);
        if (trackedPrimary == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        // 二次并发检查
        if (trackedPrimary.BeingMoved == true || trackedPrimary.Allocated == true)
        {
            _logger.LogWarning("[Gaowen] Unitload Id={Id} 二次检查不通过: BeingMoved={BeingMoved}, Allocated={Allocated}",
                primaryUnitload.UnitloadId, trackedPrimary.BeingMoved, trackedPrimary.Allocated);
            await tx.RollbackAsync();
            return false;
        }

        // 查找同库位其他 Unitload（不在移动中、未分配）
        var additionalUnitloads = await _db.Unitloads
            .Where(u => u.LocationId == locationId
                && u.UnitloadId != primaryUnitload.UnitloadId
                && u.BeingMoved != true
                && u.Allocated != true)
            .ToListAsync();

        // 构建 Ext1（所有容器码）和 Ext2（额外 UnitloadId）
        var allCodes = new List<string> { trackedPrimary.ContainerCode ?? "" };
        var additionalIds = new List<int>();
        foreach (var au in additionalUnitloads)
        {
            allCodes.Add(au.ContainerCode ?? "");
            additionalIds.Add(au.UnitloadId);
        }

        var trackedLocation = await _db.Locations.FindAsync(locationId);
        if (trackedLocation == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        var endLocation = await _db.Locations.FindAsync(endLocationId);
        if (endLocation == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        var transTask = new TransTask
        {
            TaskCode = await TaskCodeGenerator.GenerateAsync(_db),
            TaskType = Cst.出库,
            UnitloadId = primaryUnitload.UnitloadId,
            StartLocationId = locationId,
            EndLocationId = endLocationId,
            ForWcs = true,
            WasSentToWcs = false,
            Ext1 = string.Join(";", allCodes),
            Ext2 = additionalIds.Count > 0 ? string.Join(";", additionalIds) : string.Empty,
            WareHouse = endLocation.AreaName,
            LocationGroup = string.Empty
        };
        transTask.Unitload = trackedPrimary;
        transTask.StartLocation = trackedLocation;
        transTask.EndLocation = endLocation;
        _db.TransTasks.Add(transTask);

        // 标记主 Unitload
        trackedPrimary.BeingMoved = true;
        trackedPrimary.Allocated = true;

        // 标记额外 Unitload
        foreach (var au in additionalUnitloads)
        {
            au.BeingMoved = true;
            au.Allocated = true;
        }

        trackedLocation.OutboundCount++;
        endLocation.InboundCount++;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // WCS 下发（事务外，失败不影响已提交状态）
        try
        {
            await _wcsBridge.SendTaskAsync(transTask);
            transTask.WasSentToWcs = true;
            transTask.SentToWcsAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _logger.LogInformation("[Gaowen] 出库任务创建: TaskCode={TaskCode}, 主Unitload={Id}, 额外={Additional}个, Batch={Batch}",
                transTask.TaskCode, primaryUnitload.UnitloadId, additionalIds.Count, batch);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Gaowen] WCS 下发失败: TaskCode={TaskCode}, UnitloadId={Id}",
                transTask.TaskCode, primaryUnitload.UnitloadId);
            return false;
        }
    }
}
