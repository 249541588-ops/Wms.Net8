using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Handlers.WcsRequest;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 拆盘节点 — 出库后拆分多物料托盘
/// </summary>
public class SplitUnitloadHandler : INodeHandler
{
    public string NodeType => "SplitUnitload";
    public string DisplayName => "拆盘";
    public string Category => "业务逻辑";
    public string Description => "当 UnitloadItems > 1 时，拆分托盘并归档";
    public string? ConfigSchema => null;

    private readonly ILogger<SplitUnitloadHandler> _logger;

    public SplitUnitloadHandler(ILogger<SplitUnitloadHandler> logger)
    {
        _logger = logger;
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var unitload = context.Unitload;
        var transTask = context.TransTask;

        if (unitload == null || transTask == null)
            return NodeResult.Skip("无托盘或运输任务");

        // 取消时不拆盘
        if (context.IsCancelled)
            return NodeResult.Skip("任务已取消，跳过拆盘");

        // 自加载：FlowContext 的 Unitload 通常不含 UnitloadItems/Details
        if (unitload.UnitloadItems == null)
        {
            var loaded = await context.Db.Unitloads
                .Include(u => u.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                .FirstOrDefaultAsync(u => u.UnitloadId == unitload.UnitloadId);
            if (loaded == null)
                return NodeResult.Skip("加载托盘明细失败");
            context.Unitload = loaded;
            unitload = loaded;
        }

        if (unitload.UnitloadItems!.Count <= 1)
            return NodeResult.Skip("托盘只有单物料，无需拆盘");

        _logger.LogInformation("[FlowNode:SplitUnitload] 拆盘: UnitloadId={UnitloadId}, ContainerCode={ContainerCode}, Items={Count}",
            unitload.UnitloadId, unitload.ContainerCode, unitload.UnitloadItems.Count);

        var isCompletion = context.Phase == Cst.PhaseCompletion;
        if (isCompletion)
        {
            var targetLocationId = transTask.EndLocationId;
            await LocationAllocator.SplitUnitloadAsync(context.Db, unitload, targetLocationId);

            // 额外 Unitload 也拆盘（自加载 Details）
            var additional = context.Data.GetValueOrDefault("AdditionalUnitloads") as List<Unitload>;
            if (additional != null)
            {
                foreach (var au in additional)
                {
                    if (au.UnitloadItems == null || au.UnitloadItems.Count <= 1)
                        continue;

                    // 自加载 Details（additional 仅 Include 了 UnitloadItems）
                    var loadedAu = await context.Db.Unitloads
                        .Include(u => u.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                        .FirstOrDefaultAsync(u => u.UnitloadId == au.UnitloadId);
                    if (loadedAu?.UnitloadItems != null && loadedAu.UnitloadItems.Count > 1)
                    {
                        _logger.LogInformation("[FlowNode:SplitUnitload] 拆盘: UnitloadId={UnitloadId}, ContainerCode={ContainerCode}, Items={Count}",
                            loadedAu.UnitloadId, loadedAu.ContainerCode, loadedAu.UnitloadItems.Count);
                        await LocationAllocator.SplitUnitloadAsync(context.Db, loadedAu, targetLocationId);
                    }
                }
            }
        }

        return NodeResult.Ok();
    }
}
