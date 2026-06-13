using Microsoft.Extensions.Logging;
using System.Data;
using Dapper;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Tasks;

namespace Wms.Core.Infrastructure.Tasks.Rules;

/// <summary>
/// SSRule08 — 单深巷道，指定列分配
/// </summary>
public class SSRule08 : LocationAllocationRuleBase
{
    public SSRule08(ILogger<SSRule08> logger) : base(logger) { }

    public override string RuleName => "SSRule08";
    public override bool DoubleDeep => false;
    public override int Order => 130;

    protected override string BuildSql(
        int lanewayId, UnitloadStorageInfo storageInfo,
        int[] excludedIds, int[] excludedColumns, int[] excludedLevels,
        string orderBy, string sortMethod)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($@"
SELECT TOP 1 loc.*
FROM Locations loc
JOIN Racks rack ON rack.RackId = loc.RackId
JOIN Cells c ON c.CellId = loc.CellId
WHERE rack.LanewayId = @lanewayId
AND loc.xExists = 1
AND loc.UnitloadCount = 0
AND loc.OutboundCount = 0
AND loc.InboundDisabled = 0
AND loc.InboundCount = 0
AND loc.WeightLimit >= @weight
AND loc.HeightLimit >= @height
AND loc.StorageGroup = @storageGroup
AND loc.SubStorageGroup = @subStorageGroup
AND loc.xSpecification = @locSpec");

        if (excludedColumns != null && excludedColumns.Length > 0)
            sb.AppendLine("AND loc.xColumn NOT IN @excludedColumns");

        if (excludedIds != null && excludedIds.Length > 0)
            sb.AppendLine("AND loc.LocationId NOT IN @excludedIds");

        if (excludedLevels != null && excludedLevels.Length > 0)
            sb.AppendLine("AND loc.xLevel NOT IN @excludedLevels");

        sb.AppendLine($"ORDER BY loc.xLevel, loc.WeightLimit, loc.HeightLimit, {SafeOrderBy(orderBy, sortMethod)}");
        return sb.ToString();
    }

    protected override DynamicParameters BuildParameters(int lanewayId, UnitloadStorageInfo storageInfo)
    {
        var p = new DynamicParameters();
        p.Add("lanewayId", lanewayId);
        p.Add("weight", storageInfo.Weight);
        p.Add("height", storageInfo.Height);
        p.Add("storageGroup", storageInfo.StorageGroup);
        p.Add("subStorageGroup", storageInfo.SubStorageGroup);
        p.Add("locSpec", storageInfo.ContainerSpecification);
        return p;
    }
}
