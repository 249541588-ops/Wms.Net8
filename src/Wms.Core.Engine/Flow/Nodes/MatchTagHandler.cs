using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Enums;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Handlers.WcsRequest;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 工艺匹配节点 — 验证 Location.Tag 与 Unitload.NextOperation 是否匹配
/// </summary>
public class MatchTagHandler : INodeHandler
{
    public string NodeType => "MatchTag";
    public string DisplayName => "工艺匹配";
    public string Category => "验证";
    public string Description => "验证位置 Tag 与托盘下一工序（NextOperation）是否匹配";
    public string? ConfigSchema => null;

    private readonly ILogger<MatchTagHandler> _logger;

    public MatchTagHandler(ILogger<MatchTagHandler> logger)
    {
        _logger = logger;
    }

    public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var location = context.StartLocation;
        var unitload = context.Unitload;
        var containerCode = context.CurrentContainerCode;

        if (location == null)
            return Task.FromResult(NodeResult.Fail("起始位置为空"));
        if (unitload == null)
            return Task.FromResult(NodeResult.Fail("托盘为空"));

        if (string.IsNullOrWhiteSpace(location.Tag))
        {
            return Task.FromResult(NodeResult.WcsFail(
                $"位置 {location.LocationCode} 的 Tag 为空，无法匹配工艺",
                ResultCodeTypes.数据异常, -1));
        }

        if (!LocationAllocator.IsTagMatch(location.Tag, unitload.NextOperation))
        {
            return Task.FromResult(NodeResult.WcsFail(
                $"托盘 {containerCode} 工艺 {unitload.NextOperation} 与位置 Tag {location.Tag} 不匹配",
                ResultCodeTypes.数据异常, -1));
        }

        // 入库时：验证所有容器码的工艺匹配（仅标准入库单次执行；入库双叉走循环，各自迭代验证）
        if (context.FlowCategory == Cst.入库)
        {
            var validated = context.Data.GetValueOrDefault("ValidatedUnitloads") as Dictionary<string, Unitload>;
            if (validated != null)
            {
                foreach (var kvp in validated)
                {
                    if (kvp.Key == containerCode) continue;
                    if (!LocationAllocator.IsTagMatch(location.Tag, kvp.Value.NextOperation))
                    {
                        return Task.FromResult(NodeResult.WcsFail(
                            $"托盘 {kvp.Key} 工艺 {kvp.Value.NextOperation} 与位置 Tag {location.Tag} 不匹配",
                            ResultCodeTypes.数据异常, -1));
                    }
                }
            }
        }

        return Task.FromResult(NodeResult.Ok());
    }
}
