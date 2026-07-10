using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 更新托盘状态节点 — 按阶段设置 BeingMoved / Allocated / LocationId
/// </summary>
/// <remarks>
/// 请求阶段：BeingMoved=true, Allocated=true, LocationId=起始位置（仅入库）
/// 完成阶段：BeingMoved=false, Allocated=false, LocationId=终点位置
/// </remarks>
public class UpdateUnitloadHandler : INodeHandler
{
    public string NodeType => "UpdateUnitload";
    public string DisplayName => "更新托盘";
    public string Category => "状态更新";
    public string Description => "更新托盘的移动状态、分配状态和位置信息";
    public string? ConfigSchema => null;

    private readonly ILogger<UpdateUnitloadHandler> _logger;

    public UpdateUnitloadHandler(ILogger<UpdateUnitloadHandler> logger)
    {
        _logger = logger;
    }

    public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var unitload = context.Unitload;
        if (unitload == null)
            return Task.FromResult(NodeResult.Fail("托盘为空"));

        var isCompletion = context.Phase == Cst.PhaseCompletion;
        var requestType = context.FlowCategory ?? Cst.入库;

        if (isCompletion)
        {
            // 完成阶段：重置移动状态
            unitload.BeingMoved = false;
            unitload.Allocated = false;

            if (!context.IsCancelled)
            {
                // 保存原始入库时间（出库 MES 上传需要）
                if (unitload.CurrentLocationTime.HasValue)
                    context.Data["OriginalLocationTime"] = unitload.CurrentLocationTime.Value;

                // 真正完成：设置到终点位置
                if (context.TransTask != null && context.TransTask.EndLocationId > 0)
                {
                    unitload.LocationId = context.TransTask.EndLocationId;
                    unitload.CurrentLocationTime = DateTime.Now;
                }
            }
            // 取消时：位置不变，只重置状态

            // 重置额外 Unitload（标准入库多容器码场景）
            var additional = context.Data.GetValueOrDefault("AdditionalUnitloads") as List<Unitload>;
            if (additional != null)
            {
                foreach (var u in additional)
                {
                    u.BeingMoved = false;
                    u.Allocated = false;
                    if (!context.IsCancelled && context.TransTask?.EndLocationId > 0)
                    {
                        u.LocationId = context.TransTask.EndLocationId;
                        u.CurrentLocationTime = DateTime.Now;
                    }
                }
            }
        }
        else
        {
            // 请求阶段：标记占用，仅入库时设置到起始位置
            unitload.BeingMoved = true;
            unitload.Allocated = true;

            if (requestType == Cst.入库 || requestType == Cst.入库双叉)
            {
                unitload.LocationId = context.StartLocation!.LocationId;
                unitload.CurrentLocationTime = DateTime.Now;
            }

            // 入库时：更新所有验证过的 Unitload（仅标准入库；入库双叉走循环模式，各自迭代标记）
            if (requestType == Cst.入库)
            {
                var validated = context.Data.GetValueOrDefault("ValidatedUnitloads") as Dictionary<string, Unitload>;
                if (validated != null)
                {
                    foreach (var kvp in validated)
                    {
                        if (kvp.Value.UnitloadId == unitload.UnitloadId) continue;
                        kvp.Value.BeingMoved = true;
                        kvp.Value.Allocated = true;
                        kvp.Value.LocationId = context.StartLocation!.LocationId;
                        kvp.Value.CurrentLocationTime = DateTime.Now;
                    }
                }
            }
        }

        return Task.FromResult(NodeResult.Ok());
    }
}
