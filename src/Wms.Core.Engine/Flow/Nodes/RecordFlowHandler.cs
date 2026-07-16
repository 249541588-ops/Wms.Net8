using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Handlers.WcsRequest;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 记录流水节点 — 创建 Flow + UnitloadOp 记录
/// </summary>
public class RecordFlowHandler : INodeHandler
{
    public string NodeType => "RecordFlow";
    public string DisplayName => "记录流水";
    public string Category => "数据持久化";
    public string Description => "创建库存流水（Flow）和托盘操作流水（UnitloadOp）";
    public string? ConfigSchema => """
    {
      "type": "object",
      "properties": {
        "bizType": { "type": "string", "description": "业务类型：入库/出库/移库" }
      }
    }
    """;

    private readonly ILogger<RecordFlowHandler> _logger;

    public RecordFlowHandler(ILogger<RecordFlowHandler> logger)
    {
        _logger = logger;
    }

    public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var unitload = context.Unitload;
        var transTask = context.TransTask;

        if (unitload == null || transTask == null)
            return Task.FromResult(NodeResult.Skip("无托盘或运输任务"));

        // 取消时不记录流水
        if (context.IsCancelled)
            return Task.FromResult(NodeResult.Skip("任务已取消，跳过流水"));

        var isCompletion = context.Phase == Cst.PhaseCompletion;
        if (isCompletion)
        {
            // 确定业务类型（入库/出库/移库）
            var bizType = transTask.TaskType ?? context.FlowCategory ?? Cst.入库;
            var locationId = transTask.EndLocationId;

            // 创建 Flow 流水
            if (unitload.UnitloadItems != null)
            {
                foreach (var item in unitload.UnitloadItems)
                {
                    if (item.MaterialId.HasValue
                        && item.Material?.MaterialCode != CommonTypes.空托盘
                        && item.Material?.MaterialCode != CommonTypes.工装板)
                    {
                        var flow = LocationAllocator.CreateFlow(unitload, transTask, locationId, bizType, item.MaterialId.Value, item);
                        context.Db.Flows.Add(flow);
                    }
                }
            }

            // 额外 Unitload 也创建 Flow 流水
            var additional = context.Data.GetValueOrDefault("AdditionalUnitloads") as List<Unitload>;
            if (additional != null)
            {
                foreach (var u in additional)
                {
                    if (u.UnitloadItems != null)
                    {
                        foreach (var item in u.UnitloadItems)
                        {
                            if (item.MaterialId.HasValue
                                && item.Material?.MaterialCode != CommonTypes.空托盘
                                && item.Material?.MaterialCode != CommonTypes.工装板)
                            {
                                var flow = LocationAllocator.CreateFlow(u, transTask, locationId, bizType, item.MaterialId.Value, item);
                                context.Db.Flows.Add(flow);
                            }
                        }
                    }
                }
            }

            // 创建 UnitloadOp 流水
            var direction = bizType switch
            {
                Cst.出库 => Domain.Enums.UnitloadOps_Enum.Direction.出库.ToString(),
                Cst.移库 => Domain.Enums.UnitloadOps_Enum.Direction.移动.ToString(),
                _ => Domain.Enums.UnitloadOps_Enum.Direction.入库.ToString()
            };
            LocationAllocator.AddUnitloadOp(context.Db,
                unitload.ContainerCode ?? string.Empty,
                Domain.Enums.UnitloadOps_Enum.OpType.自动.ToString(),
                direction,
                $"{bizType}完成 TaskCode={transTask.TaskCode}");

            // 额外 Unitload 也创建 UnitloadOp 流水
            if (additional != null)
            {
                foreach (var u in additional)
                {
                    LocationAllocator.AddUnitloadOp(context.Db,
                        u.ContainerCode ?? string.Empty,
                        Domain.Enums.UnitloadOps_Enum.OpType.自动.ToString(),
                        direction,
                        $"{bizType}完成 TaskCode={transTask.TaskCode}");
                }
            }
        }

        return Task.FromResult(NodeResult.Ok());
    }
}
