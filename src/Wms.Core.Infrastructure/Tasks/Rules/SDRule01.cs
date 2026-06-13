using Microsoft.Extensions.Logging;
using System.Data;
using Dapper;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Tasks;

namespace Wms.Core.Infrastructure.Tasks.Rules;

/// <summary>
/// SDRule01 — 双深巷道，一深已有货（01→11）
/// </summary>
public class SDRule01 : LocationAllocationRuleBase
{
    public SDRule01(ILogger<SDRule01> logger) : base(logger) { }

    public override string RuleName => "SDRule01";
    public override bool DoubleDeep => true;
    public override int Order => 100;

    protected override string BuildSql(
        int lanewayId, UnitloadStorageInfo storageInfo,
        int[] excludedIds, int[] excludedColumns, int[] excludedLevels,
        string orderBy, string sortMethod)
    {
        var excludes = BuildExcludeClauses(excludedIds, excludedColumns, excludedLevels, "loc1");
        return $@"
SELECT TOP 1 loc1.*
FROM Locations loc1
JOIN Racks rack1 ON rack1.RackId = loc1.RackId
JOIN Cells c ON c.CellId = loc1.CellId,
     Locations loc2
JOIN Racks rack2 ON rack2.RackId = loc2.RackId
WHERE rack1.Deep = 1
AND rack2.Deep = 2
AND loc1.CellId = loc2.CellId
AND rack1.LanewayId = @lanewayId
AND loc2.xExists = 1
AND loc2.UnitloadCount > 0
AND loc2.OutboundCount = 0
AND loc2.InboundCount = 0
AND NOT EXISTS (
    SELECT 1 FROM Unitloads u
    WHERE u.LocationId = loc2.LocationId
    AND (u.OutFlag <> @outFlag OR u.Allocated = 1)
)
AND loc1.xExists = 1
AND loc1.UnitloadCount = 0
AND loc1.OutboundCount = 0
AND loc1.InboundDisabled = 0
AND loc1.InboundCount = 0
AND loc1.WeightLimit >= @weight
AND loc1.HeightLimit >= @height
AND loc1.StorageGroup = @storageGroup
AND loc1.xSpecification = @locSpec
{excludes}
ORDER BY loc1.WeightLimit, loc1.HeightLimit, {SafeOrderBy(orderBy, sortMethod)}";
    }

    protected override DynamicParameters BuildParameters(int lanewayId, UnitloadStorageInfo storageInfo)
    {
        var p = new DynamicParameters();
        p.Add("lanewayId", lanewayId);
        p.Add("outFlag", storageInfo.OutFlag ?? "N");
        p.Add("weight", storageInfo.Weight);
        p.Add("height", storageInfo.Height);
        p.Add("storageGroup", storageInfo.StorageGroup);
        p.Add("locSpec", storageInfo.ContainerSpecification);
        return p;
    }
}
