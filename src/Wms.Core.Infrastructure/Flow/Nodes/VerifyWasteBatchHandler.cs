using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Common;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 批次验证节点 — 比较多个托盘批次是否一致
/// 流程：验证容器存在 → 取批次 → 比较 → 返回 NodeResult
/// </summary>
public class VerifyWasteBatchHandler : INodeHandler
{
    public string NodeType => "VerifyWasteBatch";
    public string DisplayName => "批次验证";
    public string Category => "验证";
    public string Description => "比较多个托盘的批次是否一致";
    public string? ConfigSchema => null;

    private readonly ILogger<VerifyWasteBatchHandler> _logger;

    public VerifyWasteBatchHandler(ILogger<VerifyWasteBatchHandler> logger)
    {
        _logger = logger;
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var location = context.StartLocation;
        var request = context.WcsRequest;

        if (request?.ContainerCode == null || request.ContainerCode.Length < 2)
            return NodeResult.Fail("批次验证至少需要两个容器编码");

        var codes = request.ContainerCode
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToArray();

        if (codes.Length < 2)
            return NodeResult.Fail("有效容器编码至少需要两个");

        _logger.LogInformation("[批次验证] 位置={Location}, 容器={Container}",
            location?.LocationCode, string.Join(",", codes));

        // 1. 批量查询所有 Unitload（含 UnitloadItems）
        var unitloads = await context.Db.Unitloads
            .Include(u => u.UnitloadItems)
            .Where(u => codes.Contains(u.ContainerCode))
            .ToListAsync();

        // 2. 验证所有容器都存在
        foreach (var code in codes)
        {
            if (unitloads.All(u => u.ContainerCode != code))
                return NodeResult.Fail($"托盘 {code} 不存在");
        }

        // 3. 取每个托盘第一个 UnitloadItem 的 Batch，比较是否全部一致
        var batches = unitloads.Select(u =>
            u.UnitloadItems?.FirstOrDefault()?.Batch ?? string.Empty).ToList();

        var allSame = batches.Distinct(StringComparer.OrdinalIgnoreCase).Count() <= 1;

        if (!allSame)
        {
            var details = unitloads.Select(u =>
                $"{u.ContainerCode}({u.UnitloadItems?.FirstOrDefault()?.Batch})");
            _logger.LogWarning("[批次验证] 批次验证不通过: {Details}", string.Join(", ", details));
            return NodeResult.WcsFail(
                $"托盘批次不一致: {string.Join(", ", details)}",
                ResultCodeTypes.排废批次不同, -1);
        }

        _logger.LogInformation("[批次验证] 批次验证通过: {Containers}", string.Join(",", codes));
        return NodeResult.Ok(new Dictionary<string, object?>
        {
            ["BatchVerified"] = true,
            ["VerifiedBatch"] = batches.First()
        });
    }
}
