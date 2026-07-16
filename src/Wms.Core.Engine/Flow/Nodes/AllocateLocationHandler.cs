using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Handlers.WcsRequest;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 分配货位节点 — 调用 LocationAllocator 分配目标货位
/// </summary>
public class AllocateLocationHandler : INodeHandler
{
    public string NodeType => "AllocateLocation";
    public string DisplayName => "分配货位";
    public string Category => "业务逻辑";
    public string Description => "调用 LocationAllocator 为托盘分配目标存储位置";
    public string? ConfigSchema => null;

    private readonly LocationAllocator _allocator;
    private readonly ILogger<AllocateLocationHandler> _logger;

    public AllocateLocationHandler(LocationAllocator allocator, ILogger<AllocateLocationHandler> logger)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var location = context.StartLocation;
        var unitload = context.Unitload;

        if (location == null)
            return NodeResult.Fail("起始位置为空");
        if (unitload == null)
            return NodeResult.Fail("托盘为空");

        var isCompletion = context.Phase == Cst.PhaseCompletion;
        if (!isCompletion)
        {
            // 双叉场景：第二个容器优先临近分配
            var reference = context.Data.GetValueOrDefault("ReferenceLocation") as Location;
            Location? targetLocation;
            if (reference != null)
            {
                targetLocation = await _allocator.AllocateNearbyAsync(reference, location, unitload);
            }
            else
            {
                targetLocation = await _allocator.AllocateAsync(location, unitload);
            }
            if (targetLocation == null)
            {
                return NodeResult.WcsFail(
                    $"托盘 {context.CurrentContainerCode} 分配货位失败，无可用库位",
                    ResultCodeTypes.程序异常, -1);
            }

            // 验证起点与终点不能一样
            if (location.LocationId == targetLocation.LocationId)
            {
                return NodeResult.WcsFail(
                    $"托盘 {context.CurrentContainerCode} 起点与终点相同（{location.LocationCode}），无法入库",
                    ResultCodeTypes.数据异常, -1);
            }

            context.TargetLocation = targetLocation;
        }

        return NodeResult.Ok();
    }
}
