using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Enums;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 检查托盘状态节点 — BeingMoved / Allocated / LocationId 等
/// </summary>
public class CheckUnitloadStatusHandler : INodeHandler
{
    public string NodeType => "CheckUnitloadStatus";
    public string DisplayName => "检查托盘状态";
    public string Category => "验证";
    public string Description => "检查 Unitload 是否正在移动、已被分配、位置信息是否有效";
    public string? ConfigSchema => null;

    private readonly ILogger<CheckUnitloadStatusHandler> _logger;

    public CheckUnitloadStatusHandler(ILogger<CheckUnitloadStatusHandler> logger)
    {
        _logger = logger;
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var unitload = context.Unitload;
        if (unitload == null)
            return NodeResult.Fail("未找到托盘");

        var containerCode = context.CurrentContainerCode;

        if (unitload.BeingMoved == true)
            return NodeResult.WcsFail($"托盘 {containerCode} 正在移动中", ResultCodeTypes.任务重复, -1);

        if (unitload.Allocated == true)
            return NodeResult.WcsFail($"托盘 {containerCode} 已被分配", ResultCodeTypes.任务重复, -1);

        // 移库和出库时验证 Unitload 在请求的位置
        //if (context.StartLocation != null && unitload.LocationId != null)
        //{
        //    if (unitload.LocationId != context.StartLocation.LocationId)
        //    {
        //        return NodeResult.WcsFail(
        //            $"托盘 {containerCode} 不在位置 {context.StartLocation.LocationCode}（当前在位置 {unitload.LocationId}）",
        //            ResultCodeTypes.数据异常, -1);
        //    }
        //}

        // 移库时验证当前位置 outbound 禁用
        if (context.Unitload?.LocationId != null)
        {
            var currentLocation = await context.Db.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LocationId == unitload.LocationId.Value);

            if (currentLocation != null)
            {
                // 入库时：如果在货架位则不能入库
                if (currentLocation.LocationType == Location_Enum.LocationType.R.ToString()
                    && context.StartLocation?.RequestType == Cst.入库)
                {
                    return NodeResult.WcsFail(
                        $"托盘 {containerCode} 当前在货架位 {currentLocation.LocationCode}，无法入库",
                        ResultCodeTypes.数据异常, -1);
                }

                // 移库时：检查当前位置是否禁止出库
                if (context.StartLocation?.RequestType == Cst.移库 && currentLocation.OutboundDisabled)
                {
                    return NodeResult.WcsFail(
                        $"托盘 {containerCode} 当前位置 {currentLocation.LocationCode} 已禁止出库",
                        ResultCodeTypes.数据异常, -1);
                }
            }
        }

        // 入库时检查是否有正在执行的 WCS 任务
        if (context.StartLocation != null && context.StartLocation.RequestType == Cst.入库)
        {
            var existingTask = await context.Db.TransTasks
                .AnyAsync(t => t.UnitloadId == unitload.UnitloadId
                    && t.ForWcs == true
                    && t.WasSentToWcs != true);
            if (existingTask)
            {
                return NodeResult.WcsFail($"托盘 {containerCode} 已有任务在执行", ResultCodeTypes.任务重复, -1);
            }
        }

        // 入库时：验证所有容器码对应的 Unitload 状态
        if (context.StartLocation?.RequestType == Cst.入库)
        {
            var validated = context.Data.GetValueOrDefault("ValidatedUnitloads") as Dictionary<string, Unitload>;
            if (validated != null)
            {
                foreach (var kvp in validated)
                {
                    if (kvp.Key == containerCode) continue;
                    var other = kvp.Value;

                    if (other.BeingMoved == true)
                        return NodeResult.WcsFail($"托盘 {kvp.Key} 正在移动中", ResultCodeTypes.任务重复, -1);

                    if (other.Allocated == true)
                        return NodeResult.WcsFail($"托盘 {kvp.Key} 已被分配", ResultCodeTypes.任务重复, -1);

                    if (other.LocationId != null)
                    {
                        var currentLocation = await context.Db.Locations
                            .AsNoTracking()
                            .FirstOrDefaultAsync(l => l.LocationId == other.LocationId.Value);
                        if (currentLocation != null && currentLocation.LocationType == Location_Enum.LocationType.R.ToString())
                        {
                            return NodeResult.WcsFail(
                                $"托盘 {kvp.Key} 当前在货架位 {currentLocation.LocationCode}，无法入库",
                                ResultCodeTypes.数据异常, -1);
                        }

                        var existingTask = await context.Db.TransTasks
                            .AnyAsync(t => t.UnitloadId == other.UnitloadId
                                && t.ForWcs == true
                                && t.WasSentToWcs != true);
                        if (existingTask)
                        {
                            return NodeResult.WcsFail($"托盘 {kvp.Key} 已有任务在执行", ResultCodeTypes.任务重复, -1);
                        }
                    }
                }
            }
        }

        return NodeResult.Ok();
    }
}
