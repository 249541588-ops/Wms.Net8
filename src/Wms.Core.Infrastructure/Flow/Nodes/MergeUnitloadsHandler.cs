using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Handlers.WcsRequest;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 叠盘合并节点 — 将多个来源托盘合并到目标托盘，归档来源，更新库位计数和操作流水
/// 事务由 FlowEngine 分段管理，本节点不再自建事务
/// </summary>
public class MergeUnitloadsHandler : INodeHandler
{
    public string NodeType => "MergeUnitloads";
    public string DisplayName => "叠盘合并";
    public string Category => "业务逻辑";
    public string Description => "将来源托盘的 Items 合并到目标托盘，归档来源，更新库位计数和操作流水";
    public string? ConfigSchema => null;

    private readonly ILogger<MergeUnitloadsHandler> _logger;

    public MergeUnitloadsHandler(ILogger<MergeUnitloadsHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        // 防御性检查
        if (context.StartLocation == null)
            return NodeResult.Fail("起始位置为空");

        if (context.Data.GetValueOrDefault("StackingTarget") is not Unitload target)
            return NodeResult.Fail("上下文中无叠盘目标托盘");

        if (context.Data.GetValueOrDefault("StackingSources") is not List<Unitload> sources)
            return NodeResult.Fail("上下文中无叠盘来源托盘");

        if (context.Data.GetValueOrDefault("StackingSourceCodes") is not string[] sourceCodes)
            return NodeResult.Fail("上下文中无叠盘来源编码");

        var targetCode = target.ContainerCode ?? "";
        var locationCode = context.StartLocation.LocationCode;

        _logger.LogInformation("[MergeUnitloads] 开始叠盘: 位置={Location}, target={Target}, sources=[{Sources}]",
            locationCode, targetCode, string.Join(",", sourceCodes));

        // 事务由 FlowEngine 分段管理，本节点不再自建事务
        // 循环合并：每个 source 的 Items 转移到 target，归档 source
        foreach (var source in sources)
        {
            var sourceLocationId = source.LocationId;

            await LocationAllocator.MergeUnitloadsAsync(context.Db, target, source, "WCS叠盘");

            // 更新 source 原库位的 UnitloadCount
            if (sourceLocationId.HasValue)
            {
                var sourceLocation = await context.Db.Locations.FindAsync(sourceLocationId.Value);
                if (sourceLocation != null)
                    sourceLocation.UnitloadCount = Math.Max(0, sourceLocation.UnitloadCount - 1);
            }
        }

        // 更新 target 的位置和到位时间
        target.LocationId = context.StartLocation.LocationId;
        target.CurrentLocationTime = DateTime.Now;

        // 记录 UnitloadOp 操作流水
        LocationAllocator.AddUnitloadOp(context.Db, targetCode,
            UnitloadOps_Enum.OpType.自动.ToString(), UnitloadOps_Enum.Direction.叠盘.ToString(),
            $"WCS叠盘: {string.Join(",", sourceCodes)} → {targetCode}");

        // SaveChanges 由 FlowEngine 的分段事务在 boundary 处统一提交
        await context.Db.SaveChangesAsync();

        _logger.LogInformation("[MergeUnitloads] 叠盘完成: {Sources} → {Target}, 位置={Loc}",
            string.Join(",", sourceCodes), targetCode, locationCode);

        return NodeResult.Ok();
    }
}
