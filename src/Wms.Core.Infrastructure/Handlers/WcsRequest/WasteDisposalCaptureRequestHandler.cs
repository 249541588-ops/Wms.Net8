using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wms.Core.Application.Handlers.WcsRequest;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Persistence;
using WcsRequestDto = Wms.Core.Application.DTOs.WcsRequest;

namespace Wms.Core.Infrastructure.Handlers.WcsRequest;

/// <summary>
/// 排废工位完成请求处理器 — 删除 NG 电芯 + 级联清理空数据 + 注销空托盘
/// 流程：验证容器 → 删除 NG Details → 级联删除空 Items/Unitloads → 杭可注销托盘 → 返回 WcsResult
/// </summary>
public class WasteDisposalCaptureRequestHandler : IWcsRequestHandler
{
    private readonly WmsDbContext _db;
    private readonly IHangKeClient _hangkeClient;
    private readonly HangKeClientOptions _hangkeOptions;
    private readonly ILogger<WasteDisposalCaptureRequestHandler> _logger;

    /// <summary>
    /// 请求类型：排废更新
    /// </summary>
    public string RequestType => Cst.排废更新;

    /// <summary>
    /// </summary>
    /// <param name="db"></param>
    /// <param name="hangkeClient"></param>
    /// <param name="hangkeOptions"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public WasteDisposalCaptureRequestHandler(
        WmsDbContext db,
        IHangKeClient hangkeClient,
        IOptions<HangKeClientOptions> hangkeOptions,
        ILogger<WasteDisposalCaptureRequestHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _hangkeClient = hangkeClient ?? throw new ArgumentNullException(nameof(hangkeClient));
        _hangkeOptions = hangkeOptions?.Value ?? throw new ArgumentNullException(nameof(hangkeOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WcsResult> HandleAsync(WcsRequestDto request, Location location)
    {
        // 步骤 1: 刷新 location
        location = await _db.Locations.FindAsync(location.LocationId) ?? location;

        _logger.LogInformation("[WcsRequest] 排废完成处理: 位置={Location}, 容器={Container}",
            location.LocationCode, string.Join(",", request.ContainerCode ?? []));

        // 步骤 2: 验证基础参数
        if (request.ContainerCode == null || request.ContainerCode.Length == 0)
            return ApiResultHelper.WcsFail("容器编码不能为空", ResultCodeTypes.数据异常, -1);

        var ngDetailIds = new HashSet<int>();
        var emptyItemIds = new HashSet<int>();
        var emptyUnitloadIds = new HashSet<int>();
        var unitloadToContainer = new Dictionary<int, string>();
        var results = new List<object>();

        foreach (var containerCode in request.ContainerCode)
        {
            if (string.IsNullOrWhiteSpace(containerCode)) continue;

            // 查询 Unitload（含完整导航属性）
            var unitload = await _db.Unitloads
                .Include(u => u.UnitloadItems)!
                    .ThenInclude(ui => ui!.UnitloadItemDetails)
                .FirstOrDefaultAsync(u => u.ContainerCode == containerCode);
            if (unitload == null)
                return ApiResultHelper.WcsFail($"托盘 {containerCode} 不存在", ResultCodeTypes.数据异常, -1);

            int ngRemoved = 0;

            // 步骤 3: 遍历每个 UnitloadItem，删除 NG 状态的 Details
            foreach (var item in unitload.UnitloadItems ?? [])
            {
                var details = item.UnitloadItemDetails?.ToList() ?? [];
                int totalDetails = details.Count;

                var ngDetails = details
                    .Where(d => d.Status != Unitload_Enum.UnitloadItemDetailStatus.正常.ToString())
                    .ToList();
                ngRemoved += ngDetails.Count;

                // 收集 NG Detail ID（用查询表达式统一删除，避免遍历中修改跟踪集合）
                foreach (var ng in ngDetails)
                    ngDetailIds.Add(ng.UnitloadItemDetailId);

                var remainingDetails = totalDetails - ngDetails.Count;

                // 步骤 4: 处理空集合 — 级联删除
                // 所有 Detail 都是 NG 或原本就为空 → Item 变空，加入级联删除
                if (remainingDetails == 0)
                {
                    emptyItemIds.Add(item.UnitloadItemId);
                }
                else
                {
                    // 更新 Quantity
                    item.Quantity = remainingDetails;
                }
            }

            // 检查 Unitload 是否所有 Item 都空 → Unitload 也需删除
            var totalItems = unitload.UnitloadItems?.Count ?? 0;
            var emptyItemCountForThis = (unitload.UnitloadItems ?? [])
                .Count(ui => emptyItemIds.Contains(ui.UnitloadItemId));

            if (emptyItemCountForThis == totalItems && totalItems > 0)
            {
                emptyUnitloadIds.Add(unitload.UnitloadId);
                unitloadToContainer[unitload.UnitloadId] = containerCode;
            }

            results.Add(new
            {
                containerCode,
                ngRemoved,
                unitloadDeleted = emptyUnitloadIds.Contains(unitload.UnitloadId)
            });

            _logger.LogInformation(
                "[WcsRequest] 排废处理完成: Container={Container}, NG={NgRemoved}, UnitloadDeleted={Deleted}",
                containerCode, ngRemoved, emptyUnitloadIds.Contains(unitload.UnitloadId));
        }

        // 事务：删除数据（参照 InboundRequestHandler + LocationAllocator 模式）
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            // 按层级删除（统一使用查询表达式，复用 LocationAllocator 模式）
            // 1. 先删子：NG UnitloadItemDetails
            if (ngDetailIds.Count > 0)
                _db.Set<UnitloadItemDetail>().RemoveRange(
                    _db.Set<UnitloadItemDetail>()
                        .Where(d => ngDetailIds.Contains(d.UnitloadItemDetailId)));

            // 2. 再删父：空的 UnitloadItems
            if (emptyItemIds.Count > 0)
                _db.Set<UnitloadItem>().RemoveRange(
                    _db.Set<UnitloadItem>()
                        .Where(i => emptyItemIds.Contains(i.UnitloadItemId)));

            // 3. 删除 Unitload 前清理 FK 引用（复用 LocationAllocator 模式）
            foreach (var uid in emptyUnitloadIds)
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE Flows SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE Stocks SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE TransTasks SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE UnionUnitloadItems SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);
            }

            // 4. 最后删祖父：空的 Unitloads
            if (emptyUnitloadIds.Count > 0)
                _db.Set<Unitload>().RemoveRange(
                    _db.Set<Unitload>()
                        .Where(u => emptyUnitloadIds.Contains(u.UnitloadId)));

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WcsRequest] 排废数据处理异常");
            return ApiResultHelper.WcsFail($"排废数据处理异常: {ex.Message}", ResultCodeTypes.程序异常, -1);
        }

        // 步骤 5: 杭可注销（DB 事务提交后执行，避免 DB 回滚但杭可已注销的不一致）
        if (_hangkeOptions.Enable && emptyUnitloadIds.Count > 0)
        {
            foreach (var uid in emptyUnitloadIds)
            {
                var code = unitloadToContainer[uid];
                try
                {
                    await _hangkeClient.CancelTrayAsync(code);
                    _logger.LogInformation("[WcsRequest] 杭可托盘注销完成: {Container}", code);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[WcsRequest] 杭可托盘注销失败（不阻断流程）: {Container}", code);
                }
            }
        }

        return ApiResultHelper.WcsSuccess("排废完成", ResultCodeTypes.一, 1, data: results);
    }
}
