using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Handlers.WcsRequest;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 清理空托盘节点 — 删除空托盘 Item，若托盘变空则归档+删除 Unitload
/// </summary>
/// <remarks>
/// 出库完成时：清理 MaterialCode=="M999999999999" 的 UnitloadItem，
/// 若清理后 Unitload 无其他物料明细，则归档+删除 Unitload。
/// 处理主 Unitload 和 AdditionalUnitloads（Ext2 中的额外托盘）。
/// </remarks>
public class CleanupEmptyTrayHandler : INodeHandler
{
    public string NodeType => "CleanupEmptyTray";
    public string DisplayName => "清理空托盘";
    public string Category => "业务逻辑";
    public string Description => "删除空托盘明细，若托盘变空则归档+删除 Unitload";
    public string? ConfigSchema => null;

    private readonly ILogger<CleanupEmptyTrayHandler> _logger;

    public CleanupEmptyTrayHandler(ILogger<CleanupEmptyTrayHandler> logger)
    {
        _logger = logger;
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        if (context.Phase != Cst.PhaseCompletion)
            return NodeResult.Skip("非完成阶段，跳过");

        if (context.IsCancelled)
            return NodeResult.Skip("任务已取消，跳过");

        var taskCode = context.TransTask?.TaskCode;

        // 主 Unitload
        if (context.Unitload != null)
        {
            var deleted = await LocationAllocator.CleanupEmptyTrayItemsAsync(
                context.Db, context.Unitload, $"出库完成 TaskCode={taskCode}");
            if (deleted)
                _logger.LogInformation(
                    "[FlowNode:CleanupEmptyTray] 主托盘归档+删除: UnitloadId={Id}, Container={Code}",
                    context.Unitload.UnitloadId, context.Unitload.ContainerCode);
        }

        // 额外 Unitload（Ext2）
        var additional = context.Data.GetValueOrDefault("AdditionalUnitloads") as List<Unitload>;
        if (additional != null)
        {
            foreach (var au in additional)
            {
                var deleted = await LocationAllocator.CleanupEmptyTrayItemsAsync(
                    context.Db, au, $"出库完成 TaskCode={taskCode}");
                if (deleted)
                    _logger.LogInformation(
                        "[FlowNode:CleanupEmptyTray] 额外托盘归档+删除: UnitloadId={Id}, Container={Code}",
                        au.UnitloadId, au.ContainerCode);
            }
        }

        return NodeResult.Ok();
    }
}
