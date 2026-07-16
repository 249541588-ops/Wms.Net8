using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 排废完成处理节点 — 删除 NG 电芯 + 级联清理空数据 + 注销空托盘
/// 流程：验证容器 → 删除 NG Details → 级联删除空 Items/Unitloads → 杭可注销托盘 → 返回 NodeResult
/// 事务由 FlowEngine 分段管理，本节点不再自建事务
/// </summary>
public class WasteDisposalCaptureNode : INodeHandler
{
    public string NodeType => "WasteDisposalCapture";
    public string DisplayName => "排废完成处理";
    public string Category => "业务逻辑";
    public string Description => "排废工位完成处理 — 删除 NG 电芯 + 级联清理空数据 + 注销空托盘";
    public string? ConfigSchema => null;

    private readonly IHangKeClient _hangkeClient;
    private readonly HangKeClientOptions _hangkeOptions;
    private readonly ILogger<WasteDisposalCaptureNode> _logger;

    public WasteDisposalCaptureNode(
        IHangKeClient hangkeClient,
        IOptions<HangKeClientOptions> hangkeOptions,
        ILogger<WasteDisposalCaptureNode> logger)
    {
        _hangkeClient = hangkeClient ?? throw new ArgumentNullException(nameof(hangkeClient));
        _hangkeOptions = hangkeOptions?.Value ?? throw new ArgumentNullException(nameof(hangkeOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var location = context.StartLocation;
        var request = context.WcsRequest;

        if (request?.ContainerCode == null || request.ContainerCode.Length == 0)
            return NodeResult.WcsFail("容器编码不能为空", ResultCodeTypes.数据异常, -1);

        _logger.LogInformation("[FlowNode:WasteDisposalCapture] 排废完成处理: 位置={Location}, 容器={Container}",
            location?.LocationCode, string.Join(",", request.ContainerCode));

        var ngDetailIds = new HashSet<int>();
        var emptyItemIds = new HashSet<int>();
        var emptyUnitloadIds = new HashSet<int>();
        var unitloadToContainer = new Dictionary<int, string>();
        var results = new List<object>();

        foreach (var containerCode in request.ContainerCode)
        {
            if (string.IsNullOrWhiteSpace(containerCode)) continue;

            // 查询 Unitload（含完整导航属性）
            var unitload = await context.Db.Unitloads
                .Include(u => u.UnitloadItems)!
                    .ThenInclude(ui => ui!.UnitloadItemDetails)
                .FirstOrDefaultAsync(u => u.ContainerCode == containerCode);
            if (unitload == null)
                return NodeResult.WcsFail($"托盘 {containerCode} 不存在", ResultCodeTypes.数据异常, -1);

            int ngRemoved = 0;

            // 遍历每个 UnitloadItem，删除 NG 状态的 Details
            foreach (var item in unitload.UnitloadItems ?? [])
            {
                var details = item.UnitloadItemDetails?.ToList() ?? [];
                int totalDetails = details.Count;

                var ngDetails = details
                    .Where(d => d.Status != Unitload_Enum.UnitloadItemDetailStatus.正常.ToString())
                    .ToList();
                ngRemoved += ngDetails.Count;

                // 收集 NG Detail ID
                foreach (var ng in ngDetails)
                    ngDetailIds.Add(ng.UnitloadItemDetailId);

                var remainingDetails = totalDetails - ngDetails.Count;

                // 处理空集合 — 级联删除
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
                "[FlowNode:WasteDisposalCapture] 排废处理完成: Container={Container}, NG={NgRemoved}, UnitloadDeleted={Deleted}",
                containerCode, ngRemoved, emptyUnitloadIds.Contains(unitload.UnitloadId));
        }

        // DB 删除操作（事务由 FlowEngine 管理，本节点不再自建事务）
        try
        {
            // 按层级删除
            // 1. 先删子：NG UnitloadItemDetails
            if (ngDetailIds.Count > 0)
                context.Db.Set<UnitloadItemDetail>().RemoveRange(
                    context.Db.Set<UnitloadItemDetail>()
                        .Where(d => ngDetailIds.Contains(d.UnitloadItemDetailId)));

            // 2. 再删父：空的 UnitloadItems
            if (emptyItemIds.Count > 0)
                context.Db.Set<UnitloadItem>().RemoveRange(
                    context.Db.Set<UnitloadItem>()
                        .Where(i => emptyItemIds.Contains(i.UnitloadItemId)));

            // 3. 删除 Unitload 前清理 FK 引用
            foreach (var uid in emptyUnitloadIds)
            {
                await context.Db.Database.ExecuteSqlRawAsync(
                    "UPDATE Flows SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);
                await context.Db.Database.ExecuteSqlRawAsync(
                    "UPDATE Stocks SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);
                await context.Db.Database.ExecuteSqlRawAsync(
                    "UPDATE TransTasks SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);
                await context.Db.Database.ExecuteSqlRawAsync(
                    "UPDATE UnionUnitloadItems SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);
            }

            // 4. 最后删祖父：空的 Unitloads
            if (emptyUnitloadIds.Count > 0)
                context.Db.Set<Unitload>().RemoveRange(
                    context.Db.Set<Unitload>()
                        .Where(u => emptyUnitloadIds.Contains(u.UnitloadId)));

            // SaveChanges 由 FlowEngine 的分段事务在 boundary 处统一提交
            await context.Db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FlowNode:WasteDisposalCapture] 排废数据处理异常");
            return NodeResult.WcsFail($"排废数据处理异常: {ex.Message}", ResultCodeTypes.程序异常, -1);
        }

        // 事务后：杭可注销
        if (_hangkeOptions.Enable && emptyUnitloadIds.Count > 0)
        {
            foreach (var uid in emptyUnitloadIds)
            {
                var code = unitloadToContainer[uid];
                try
                {
                    await _hangkeClient.CancelTrayAsync(code);
                    _logger.LogInformation("[FlowNode:WasteDisposalCapture] 杭可托盘注销完成: {Container}", code);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[FlowNode:WasteDisposalCapture] 杭可托盘注销失败（不阻断流程）: {Container}", code);
                }
            }
        }

        return NodeResult.Ok(new Dictionary<string, object?>
        {
            ["WasteDisposalCaptureResults"] = results
        });
    }
}
