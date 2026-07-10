using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.Handlers.WcsRequest;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Persistence;
using WcsRequestDto = Wms.Core.Application.DTOs.WcsRequest;
using Wms.Core.Domain.Enums;

namespace Wms.Core.Infrastructure.Handlers.WcsRequest;

/// <summary>
/// 叠盘工位请求处理器 — 多个托盘合并成一个托盘，不下发 WCS
/// 流程：验证容器存在 → 合并 UnitloadItems → 归档 source → 返回 WcsResult
/// </summary>
public class StackingPalletRequestHandler : IWcsRequestHandler
{
    private readonly WmsDbContext _db;
    private readonly ILogger<StackingPalletRequestHandler> _logger;

    /// <summary>
    ///
    /// </summary>
    public string RequestType => Cst.叠盘;

    /// <summary>
    ///
    /// </summary>
    /// <param name="db"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public StackingPalletRequestHandler(
        WmsDbContext db,
        ILogger<StackingPalletRequestHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
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
        _logger.LogInformation("[WcsRequest] 叠盘: 位置={Location}, 容器={Container}",
            location.LocationCode, string.Join(",", request.ContainerCode ?? []));

        if (request.ContainerCode == null || request.ContainerCode.Length < 2)
            return ApiResultHelper.WcsFail("叠盘至少需要两个容器编码", ResultCodeTypes.数据异常, -1);

        var codes = request.ContainerCode
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToArray();

        if (codes.Length < 2)
            return ApiResultHelper.WcsFail("有效容器编码至少需要两个", ResultCodeTypes.数据异常, -1);

        // ★ 事务提前开启，避免查询与操作之间的竞态窗口
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. 事务内查询 location
            location = await _db.Locations.FindAsync(location.LocationId) ?? location;

            // 2. 最后一个容器码为 target，其余为 source
            var targetCode = codes[^1];
            var sourceCodes = codes[..^1];

            // 3. 查询 target（含 UnitloadItems）
            var target = await _db.Unitloads
                .Include(u => u.UnitloadItems)
                .FirstOrDefaultAsync(u => u.ContainerCode == targetCode);
            if (target == null)
                return ApiResultHelper.WcsFail($"目标托盘 {targetCode} 不存在", ResultCodeTypes.数据异常, -1);

            // 4. 查询 source Unitloads（含 UnitloadItems + UnitloadItemDetails）
            var sources = await _db.Unitloads
                .Include(u => u.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                .Where(u => sourceCodes.Contains(u.ContainerCode))
                .ToListAsync();

            // 5. 校验所有 source 都存在且有 Items
            foreach (var code in sourceCodes)
            {
                var source = sources.FirstOrDefault(u => u.ContainerCode == code);
                if (source == null)
                    return ApiResultHelper.WcsFail($"来源托盘 {code} 不存在", ResultCodeTypes.数据异常, -1);
                if (source.UnitloadItems == null || source.UnitloadItems.Count == 0)
                    return ApiResultHelper.WcsFail($"来源托盘 {code} 无物料明细，无需叠盘", ResultCodeTypes.数据异常, -1);
            }

            // 6. 校验所有 Unitload 关联位置无未完成 TransTask
            var allUnitloads = sources.Append(target).ToList();
            var allLocationIds = allUnitloads
                .Where(u => u.LocationId.HasValue)
                .Select(u => u.LocationId!.Value)
                .Distinct()
                .ToList();
            var activeTaskCount = await _db.TransTasks.CountAsync(t =>
                allLocationIds.Contains(t.StartLocationId) || allLocationIds.Contains(t.EndLocationId));
            if (activeTaskCount > 0)
                return ApiResultHelper.WcsFail($"关联 {activeTaskCount} 个未完成任务，不允许叠盘", ResultCodeTypes.数据异常, -1);

            // 7. 循环合并：每个 source 的 Items 转移到 target，归档 source
            foreach (var source in sources)
            {
                var sourceLocationId = source.LocationId;

                await LocationAllocator.MergeUnitloadsAsync(_db, target, source, "WCS叠盘");

                // 更新 source 原库位的 UnitloadCount
                if (sourceLocationId.HasValue)
                {
                    var sourceLocation = await _db.Locations.FindAsync(sourceLocationId.Value);
                    if (sourceLocation != null)
                        sourceLocation.UnitloadCount = Math.Max(0, sourceLocation.UnitloadCount - 1);
                }
            }

            // 8. 更新 target 的位置和到位时间
            target.LocationId = location.LocationId;
            target.CurrentLocationTime = DateTime.Now;

            // 9. 记录 UnitloadOp 操作流水
            LocationAllocator.AddUnitloadOp(_db, target.ContainerCode ?? "",
                UnitloadOps_Enum.OpType.自动.ToString(), UnitloadOps_Enum.Direction.叠盘.ToString(),
                $"WCS叠盘: {string.Join(",", sourceCodes)} → {targetCode}");

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation("[WcsRequest] 叠盘完成: {Sources} → {Target}, 位置={Loc}",
                string.Join(",", sourceCodes), targetCode, location.LocationCode);

            return ApiResultHelper.WcsSuccess("叠盘成功", ResultCodeTypes.一, 1);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
