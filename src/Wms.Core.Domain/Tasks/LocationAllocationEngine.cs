using System.Data;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Entities.Warehouse;

namespace Wms.Core.Domain.Tasks;

/// <summary>
/// 库位分配策略引擎 — 按规则优先级依次尝试分配
/// </summary>
public class LocationAllocationEngine
{
    private readonly ILogger<LocationAllocationEngine> _logger;
    private readonly IEnumerable<ILocationAllocationRule> _rules;

    /// <summary>
    /// 初始化库位分配引擎
    /// </summary>
    /// <param name="rules">所有已注册的分配规则</param>
    /// <param name="logger">日志</param>
    public LocationAllocationEngine(IEnumerable<ILocationAllocationRule> rules, ILogger<LocationAllocationEngine> logger)
    {
        _rules = rules.OrderBy(r => r.Order);
        _logger = logger;
    }

    /// <summary>
    /// 在指定巷道中分配一个库位
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <param name="lanewayId">巷道ID</param>
    /// <param name="doubleDeep">是否为双深巷道</param>
    /// <param name="storageInfo">入库货物信息</param>
    /// <param name="excludedIds">排除的库位ID</param>
    /// <param name="excludedColumns">排除的列</param>
    /// <param name="excludedLevels">排除的层</param>
    /// <param name="orderBy">排序字段</param>
    /// <param name="sortMethod">排序方式（ASC/DESC）</param>
    /// <returns>分配的库位，null 表示所有规则均未找到合适库位</returns>
    public async Task<Location?> AllocateAsync(
        IDbConnection connection,
        int lanewayId,
        bool doubleDeep,
        UnitloadStorageInfo storageInfo,
        int[] excludedIds,
        int[] excludedColumns,
        int[] excludedLevels,
        string orderBy,
        string sortMethod)
    {
        var applicableRules = _rules.Where(r => r.DoubleDeep == doubleDeep).OrderBy(r => r.Order);

        foreach (var rule in applicableRules)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var location = await rule.SelectAsync(connection, lanewayId, storageInfo,
                    excludedIds, excludedColumns, excludedLevels, orderBy, sortMethod);
                sw.Stop();

                if (location != null)
                {
                    _logger.LogInformation("[库位分配] 规则 {Rule} 命中，库位 {LocationId}，耗时 {Duration}ms",
                        rule.RuleName, location.LocationId, sw.ElapsedMilliseconds);
                    return location;
                }

                _logger.LogDebug("[库位分配] 规则 {Rule} 未命中，耗时 {Duration}ms",
                    rule.RuleName, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[库位分配] 规则 {Rule} 执行异常", rule.RuleName);
            }
        }

        _logger.LogWarning("[库位分配] 所有规则均未命中，巷道 {Laneway}", lanewayId);
        return null;
    }
}
