using Microsoft.Extensions.Logging;
using System.Data;
using Dapper;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Tasks;

namespace Wms.Core.Infrastructure.Tasks.Rules;

/// <summary>
/// SSRule02 — 单深巷道，同工艺匹配（loc2有货且可出库，分配到loc1）
/// </summary>
public class SSRule02 : LocationAllocationRuleBase
{
    public SSRule02(ILogger<SSRule02> logger) : base(logger) { }

    public override string RuleName => "SSRule02";
    public override bool DoubleDeep => false;
    public override int Order => 200;

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
WHERE loc1.Tag = 2
AND loc2.Tag = 1
AND loc1.CellId = loc2.CellId
AND rack1.LanewayId = @lanewayId
AND loc2.xExists = 1
AND loc2.UnitloadCount >= 0
AND loc2.OutboundCount = 0
AND loc2.InboundCount >= 0
AND loc2.LocationType = 'R'
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
AND loc1.SubStorageGroup = @subStorageGroup
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
        p.Add("subStorageGroup", storageInfo.SubStorageGroup);
        p.Add("locSpec", storageInfo.ContainerSpecification);
        return p;
    }
}
