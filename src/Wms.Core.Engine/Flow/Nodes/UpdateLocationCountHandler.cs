using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 更新库位计数节点 — InboundCount / OutboundCount（按任务类型区分）
/// </summary>
public class UpdateLocationCountHandler : INodeHandler
{
    public string NodeType => "UpdateLocationCount";
    public string DisplayName => "更新库位计数";
    public string Category => "状态更新";
    public string Description => "按任务类型更新起始/目标位置的入库计数和出库计数";
    public string? ConfigSchema => null;

    private readonly ILogger<UpdateLocationCountHandler> _logger;

    public UpdateLocationCountHandler(ILogger<UpdateLocationCountHandler> logger)
    {
        _logger = logger;
    }

    public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var startLocation = context.StartLocation;
        var targetLocation = context.TargetLocation;
        // 通过流程分类判断业务方向
        var isInbound = context.FlowCategory == Cst.入库 || context.FlowCategory == Cst.入库双叉;
        var isCompletion = context.Phase == Cst.PhaseCompletion;

        if (isInbound)
        {
            if (isCompletion)
            {
                // 完成阶段：起点 OutboundCount--，终点 InboundCount--
                if (startLocation != null)
                    startLocation.OutboundCount = Math.Max(0, startLocation.OutboundCount - 1);
                if (targetLocation != null)
                {
                    targetLocation.InboundCount = Math.Max(0, targetLocation.InboundCount - 1);

                    // 仅真正完成时：终点托盘数按实际增加（入库可能有多个托盘）
                    if (!context.IsCancelled)
                    {
                        var additional = context.Data.GetValueOrDefault("AdditionalUnitloads") as List<Unitload>;
                        var count = 1 + (additional?.Count ?? 0);
                        targetLocation.UnitloadCount += count;
                    }
                }
            }
            else
            {
                // 请求阶段：起点 OutboundCount++，终点 InboundCount++
                if (startLocation != null)
                    startLocation.OutboundCount++;
                if (targetLocation != null)
                    targetLocation.InboundCount++;
            }
        }
        else
        {
            if (isCompletion)
            {
                // 完成阶段：起点 OutboundCount--
                if (startLocation != null)
                    startLocation.OutboundCount = Math.Max(0, startLocation.OutboundCount - 1);

                // 仅真正完成时：起点托盘数按实际减少（出库可能有多个托盘）
                if (!context.IsCancelled)
                {
                    var additional = context.Data.GetValueOrDefault("AdditionalUnitloads") as List<Unitload>;
                    var count = 1 + (additional?.Count ?? 0);
                    startLocation.UnitloadCount = Math.Max(0, startLocation.UnitloadCount - count);
                }

                // 出库终点 InboundCount--（起点≠终点时，如定时器创建的任务）
                if (targetLocation != null
                    && (startLocation == null || startLocation.LocationId != targetLocation.LocationId))
                {
                    targetLocation.InboundCount = Math.Max(0, targetLocation.InboundCount - 1);
                }
            }
            else
            {
                // 请求阶段：起点 OutboundCount++
                if (startLocation != null)
                    startLocation.OutboundCount++;

                // 出库终点 InboundCount++（起点≠终点时）
                if (targetLocation != null
                    && (startLocation == null || startLocation.LocationId != targetLocation.LocationId))
                {
                    targetLocation.InboundCount++;
                }
            }
        }

        return Task.FromResult(NodeResult.Ok());
    }
}
