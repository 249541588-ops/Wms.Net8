using System.Data;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Tasks;

namespace Wms.Core.Infrastructure.Tasks.Rules;

/// <summary>
/// 库位分配规则基类 — 提供通用 SQL 构建和查询执行逻辑
/// </summary>
public abstract class LocationAllocationRuleBase : ILocationAllocationRule
{
    private readonly ILogger _logger;

    protected LocationAllocationRuleBase(ILogger logger)
    {
        _logger = logger;
    }

    public abstract string RuleName { get; }
    public abstract bool DoubleDeep { get; }
    public abstract int Order { get; }

    /// <summary>
    /// 子类实现：构建完整的查询 SQL
    /// </summary>
    protected abstract string BuildSql(
        int lanewayId,
        UnitloadStorageInfo storageInfo,
        int[] excludedIds,
        int[] excludedColumns,
        int[] excludedLevels,
        string orderBy,
        string sortMethod);

    /// <summary>
    /// 子类实现：构建查询参数
    /// </summary>
    protected abstract DynamicParameters BuildParameters(
        int lanewayId,
        UnitloadStorageInfo storageInfo);

    /// <summary>
    /// 执行规则，返回分配的库位
    /// </summary>
    public Task<Location?> SelectAsync(
        IDbConnection connection,
        int lanewayId,
        UnitloadStorageInfo storageInfo,
        int[] excludedIds,
        int[] excludedColumns,
        int[] excludedLevels,
        string orderBy,
        string sortMethod)
    {
        if (storageInfo == null)
            throw new ArgumentNullException(nameof(storageInfo));
        if (string.IsNullOrWhiteSpace(orderBy))
            throw new ArgumentException("参数 orderBy 不能为空");

        var sql = BuildSql(lanewayId, storageInfo, excludedIds, excludedColumns, excludedLevels, orderBy, sortMethod);
        var parameters = BuildParameters(lanewayId, storageInfo);

        _logger.LogWarning("[库位分配] 规则 {Rule} SQL:\n{Sql}\n参数: lanewayId={LanewayId}, weight={Weight}, height={Height}, storageGroup={StorageGroup}, subStorageGroup={SubStorageGroup}, locSpec={LocSpec} \n\n",
            RuleName, sql, lanewayId, storageInfo.Weight, storageInfo.Height,
            storageInfo.StorageGroup, storageInfo.SubStorageGroup ?? "(null)", storageInfo.ContainerSpecification ?? "(null)");

        return connection.QueryFirstOrDefaultAsync<Location?>(sql, parameters);
    }

    /// <summary>
    /// 通用辅助：构建排除条件 WHERE 子句
    /// </summary>
    protected static string BuildExcludeClauses(
        int[] excludedIds,
        int[] excludedColumns,
        int[] excludedLevels,
        string alias = "loc")
    {
        var sb = new StringBuilder();

        if (excludedIds != null && excludedIds.Length > 0)
        {
            sb.AppendLine($"AND {alias}.LocationId NOT IN @excludedIds");
        }
        if (excludedColumns != null && excludedColumns.Length > 0)
        {
            sb.AppendLine($"AND {alias}.xColumn NOT IN @excludedColumns");
        }
        if (excludedLevels != null && excludedLevels.Length > 0)
        {
            sb.AppendLine($"AND {alias}.xLevel NOT IN @excludedLevels");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 通用辅助：添加排除参数
    /// </summary>
    protected static void AddExcludeParameters(
        DynamicParameters parameters,
        int[] excludedIds,
        int[] excludedColumns,
        int[] excludedLevels)
    {
        if (excludedIds != null && excludedIds.Length > 0)
            parameters.Add("excludedIds", excludedIds);
        if (excludedColumns != null && excludedColumns.Length > 0)
            parameters.Add("excludedColumns", excludedColumns);
        if (excludedLevels != null && excludedLevels.Length > 0)
            parameters.Add("excludedLevels", excludedLevels);
    }

    /// <summary>
    /// 通用辅助：安全拼接排序字段（仅允许字母/数字/下划线）
    /// </summary>
    protected static string SafeOrderBy(string orderBy, string sortMethod)
    {
        var direction = sortMethod.ToUpperInvariant() == "DESC" ? "DESC" : "ASC";
        // 支持逗号分隔的多字段排序，如 "xColumn, xLevel"
        var fields = orderBy.Split(',');
        var parts = fields.Select(f => $"{SanitizeIdentifier(f.Trim())} {direction}");
        return string.Join(", ", parts);
    }

    private static string SanitizeIdentifier(string name)
    {
        // 仅保留字母/数字/下划线
        var result = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
                result.Append(c);
        }
        return result.Length > 0 ? result.ToString() : "LocationId";
    }
}
