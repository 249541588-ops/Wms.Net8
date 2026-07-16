using Microsoft.Extensions.Logging;
using System.Data;
using Dapper;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Tasks;

namespace Wms.Core.Infrastructure.Tasks.Rules;

/// <summary>
/// SDRule03 — 双深巷道，二深有指定出库标记
/// </summary>
public class SDRule03 : LocationAllocationRuleBase
{
    public SDRule03(ILogger<SDRule03> logger) : base(logger) { }

    public override string RuleName => "SDRule03";
    public override bool DoubleDeep => true;
    public override int Order => 300;

    protected override string BuildSql(
        int lanewayId, UnitloadStorageInfo storageInfo,
        int[] excludedIds, int[] excludedColumns, int[] excludedLevels,
        string orderBy, string sortMethod)
    {
        var excludes = BuildExcludeClauses(excludedIds, excludedColumns, excludedLevels, "loc2");
        return $@"
SELECT TOP 1 loc2.*
FROM Locations loc1
JOIN Racks rack1 ON rack1.RackId = loc1.RackId
JOIN Cells c ON c.CellId = loc1.CellId,
     Locations loc2
JOIN Racks rack2 ON rack2.RackId = loc2.RackId
WHERE rack1.Deep = 1
AND rack2.Deep = 2
AND loc1.CellId = loc2.CellId
AND rack1.LanewayId = @lanewayId
AND loc1.xExists = 1
AND loc1.UnitloadCount = 0
AND loc1.OutboundCount = 0
AND loc1.InboundCount = 0
AND NOT EXISTS (
    SELECT 1 FROM Unitloads u
    WHERE u.LocationId = loc1.LocationId
    AND u.OutFlag = @outFlag
)
AND loc2.xExists = 1
AND loc2.UnitloadCount = 0
AND loc2.OutboundCount = 0
AND loc2.InboundDisabled = 0
AND loc2.InboundCount = 0
AND loc2.WeightLimit >= @weight
AND loc2.HeightLimit >= @height
AND loc2.StorageGroup = @storageGroup
AND loc2.SubStorageGroup = @subStorageGroup
AND loc2.xSpecification = @locSpec
{excludes}
ORDER BY loc2.WeightLimit, loc2.HeightLimit, {SafeOrderBy(orderBy, sortMethod)}";
    }

    protected override DynamicParameters BuildParameters(int lanewayId, UnitloadStorageInfo storageInfo)
    {
        var p = new DynamicParameters();
        p.Add("lanewayId", lanewayId);
        p.Add("outFlag", storageInfo.OutFlag ?? "N");
        p.Add("weight", storageInfo.Weight);
        p.Add("height", storageInfo.Height);
        p.Add("storageGroup", storageInfo.StorageGroup);
        p.Add("subStorageGroup", storageInfo.SubStorageGroup);
        p.Add("locSpec", storageInfo.ContainerSpecification);
        return p;
    }
}
