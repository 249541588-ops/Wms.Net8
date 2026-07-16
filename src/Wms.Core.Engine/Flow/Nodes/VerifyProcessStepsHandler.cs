using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Common;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 工艺次数验证节点 — 比较多个托盘工艺次数是否一致
/// 流程：验证容器存在 → 取工艺次数 → 比较 → 返回 NodeResult
/// </summary>
public class VerifyProcessStepsHandler : INodeHandler
{
    public string NodeType => "VerifyProcessSteps";
    public string DisplayName => "工艺次数验证";
    public string Category => "验证";
    public string Description => "比较多个托盘的工艺次数（OperationNumber）是否一致";
    public string? ConfigSchema => null;

    private readonly ILogger<VerifyProcessStepsHandler> _logger;

    public VerifyProcessStepsHandler(ILogger<VerifyProcessStepsHandler> logger)
    {
        _logger = logger;
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var location = context.StartLocation;
        var request = context.WcsRequest;

        if (request?.ContainerCode == null || request.ContainerCode.Length < 2)
            return NodeResult.Fail("工艺次数验证至少需要两个容器编码");

        var codes = request.ContainerCode
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToArray();

        if (codes.Length < 2)
            return NodeResult.Fail("有效容器编码至少需要两个");

        _logger.LogInformation("[工艺次数验证] 位置={Location}, 容器={Container}",
            location?.LocationCode, string.Join(",", codes));

        // 1. 批量查询所有 Unitload
        var unitloads = await context.Db.Unitloads
            .Where(u => codes.Contains(u.ContainerCode))
            .ToListAsync();

        // 2. 验证所有容器都存在
        foreach (var code in codes)
        {
            if (unitloads.All(u => u.ContainerCode != code))
                return NodeResult.Fail($"托盘 {code} 不存在");
        }

        // 3. 取每个托盘的 OperationNumber，比较是否全部一致
        var operations = unitloads.Select(u =>
            u.OperationNumber ?? 0).ToList();

        var allSame = operations.Distinct().Count() <= 1;

        if (!allSame)
        {
            var details = unitloads.Select(u =>
                $"{u.ContainerCode}({u.OperationNumber})");
            _logger.LogWarning("[工艺次数验证] 工艺次数验证不通过: {Details}", string.Join(", ", details));
            return NodeResult.WcsFail(
                $"托盘工艺次数不一致: {string.Join(", ", details)}",
                ResultCodeTypes.排废批次不同, -1);
        }

        _logger.LogInformation("[工艺次数验证] 工艺次数验证通过: {Containers}", string.Join(",", codes));
        return NodeResult.Ok();
    }
}
