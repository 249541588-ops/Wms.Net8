using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Enums;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 检查库位限制节点 — 验证 InboundLimit / OutboundLimit
/// </summary>
public class CheckLocationLimitHandler : INodeHandler
{
    public string NodeType => "CheckLocationLimit";
    public string DisplayName => "检查库位限制";
    public string Category => "验证";
    public string Description => "检查位置的入库/出库计数是否已达上限";
    public string? ConfigSchema => null;

    private readonly ILogger<CheckLocationLimitHandler> _logger;

    public CheckLocationLimitHandler(ILogger<CheckLocationLimitHandler> logger)
    {
        _logger = logger;
    }

    public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var location = context.StartLocation;
        if (location == null)
            return Task.FromResult(NodeResult.Fail("起始位置为空"));

        var requestType = context.FlowCategory;

        // 入库时检查 InboundDisabled + InboundLimit
        if (requestType == Cst.入库 || requestType == Cst.入库双叉)
        {
            if (location.InboundDisabled)
                return Task.FromResult(NodeResult.WcsFail(
                    $"位置 {location.LocationCode} 已禁止入库",
                    ResultCodeTypes.数据异常, -1));

            if (location.InboundCount >= location.InboundLimit)
                return Task.FromResult(NodeResult.WcsFail(
                    $"位置 {location.LocationCode} 入库任务数已达上限 ({location.InboundCount}/{location.InboundLimit})",
                    ResultCodeTypes.数据异常, -1));
        }

        // 出库时检查 OutboundDisabled + OutboundLimit
        if (requestType == Cst.出库)
        {
            if (location.OutboundDisabled)
                return Task.FromResult(NodeResult.WcsFail(
                    $"位置 {location.LocationCode} 已禁止出库",
                    ResultCodeTypes.数据异常, -1));

            if (location.OutboundCount >= location.OutboundLimit)
                return Task.FromResult(NodeResult.WcsFail(
                    $"位置 {location.LocationCode} 出库任务数已达上限 ({location.OutboundCount}/{location.OutboundLimit})",
                    ResultCodeTypes.数据异常, -1));
        }

        return Task.FromResult(NodeResult.Ok());
    }
}
