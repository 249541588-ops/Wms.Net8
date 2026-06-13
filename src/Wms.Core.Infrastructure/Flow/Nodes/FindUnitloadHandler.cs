using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Enums;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 查托盘节点 — 按容器码查找 Unitload
/// </summary>
public class FindUnitloadHandler : INodeHandler
{
    public string NodeType => "FindUnitload";
    public string DisplayName => "查托盘";
    public string Category => "数据查询";
    public string Description => "按容器码（ContainerCode）查找 Unitload，入库时支持按 BoxCode 回退查找";
    public string? ConfigSchema => null;

    private readonly ILogger<FindUnitloadHandler> _logger;

    public FindUnitloadHandler(ILogger<FindUnitloadHandler> logger)
    {
        _logger = logger;
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var containerCode = context.CurrentContainerCode;
        _logger.LogInformation("[FindUnitload] 开始查找, ContainerCode={ContainerCode}", containerCode ?? "(null)");

        if (string.IsNullOrWhiteSpace(containerCode))
            return NodeResult.Fail("当前容器码为空");

        // 先按 ContainerCode 查
        var unitload = await context.Db.Unitloads
            .FirstOrDefaultAsync(u => u.ContainerCode == containerCode);

        _logger.LogInformation("[FindUnitload] ContainerCode={ContainerCode}, 直接查询结果={Result}",
            containerCode, unitload != null ? $"找到(UnitloadId={unitload.UnitloadId})" : "未找到");

        // 入库时支持按 BoxCode 回退查找
        if (unitload == null && context.WcsRequest != null)
        {
            var item = await context.Db.UnitloadItems
                .FirstOrDefaultAsync(i => i.BoxCode == containerCode);
            if (item?.UnitloadId != null)
            {
                unitload = await context.Db.Unitloads
                    .FirstOrDefaultAsync(u => u.UnitloadId == item.UnitloadId.Value);
                _logger.LogInformation("[FindUnitload] BoxCode回退查找成功, UnitloadId={UnitloadId}", unitload?.UnitloadId);
            }
        }

        if (unitload == null)
        {
            _logger.LogWarning("[FindUnitload] 托盘 {ContainerCode} 不存在", containerCode);
            return NodeResult.WcsFail($"托盘 {containerCode} 不存在", ResultCodeTypes.数据异常, -1);
        }

        context.Unitload = unitload;
        _logger.LogInformation("[FindUnitload] 成功设置 context.Unitload, UnitloadId={UnitloadId}", unitload.UnitloadId);

        // 入库时：验证所有容器码都存在对应的 Unitload，并缓存供后续节点使用
        if (context.StartLocation?.RequestType == Cst.入库 && context.WcsRequest?.ContainerCode != null)
        {
            var allUnitloads = new Dictionary<string, Unitload> { [containerCode] = unitload };
            foreach (var cc in context.WcsRequest.ContainerCode)
            {
                if (string.IsNullOrWhiteSpace(cc) || cc == containerCode) continue;
                var other = await FindUnitloadByCodeAsync(context.Db, cc);
                if (other == null)
                {
                    _logger.LogWarning("[FindUnitload] 托盘 {ContainerCode} 不存在", cc);
                    return NodeResult.WcsFail($"托盘 {cc} 不存在", ResultCodeTypes.数据异常, -1);
                }
                allUnitloads[cc] = other;
            }
            context.Data["ValidatedUnitloads"] = allUnitloads;
        }

        return NodeResult.Ok();
    }

    /// <summary>
    /// 按容器码查找 Unitload（先按 ContainerCode 查，查不到再按 BoxCode 查）
    /// </summary>
    private static async Task<Unitload?> FindUnitloadByCodeAsync(WmsDbContext db, string code)
    {
        var unitload = await db.Unitloads.FirstOrDefaultAsync(u => u.ContainerCode == code);
        if (unitload == null)
        {
            var item = await db.UnitloadItems.FirstOrDefaultAsync(i => i.BoxCode == code);
            if (item?.UnitloadId != null)
                unitload = await db.Unitloads.FirstOrDefaultAsync(u => u.UnitloadId == item.UnitloadId.Value);
        }
        return unitload;
    }
}
