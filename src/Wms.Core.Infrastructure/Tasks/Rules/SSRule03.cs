using Microsoft.Extensions.Logging;
using System.Data;
using Dapper;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Tasks;

namespace Wms.Core.Infrastructure.Tasks.Rules;

/// <summary>
/// SSRule03 — 单深巷道，全新货位（loc1和loc2都空）
/// </summary>
public class SSRule03 : LocationAllocationRuleBase
{
    public SSRule03(ILogger<SSRule03> logger) : base(logger) { }

    public override string RuleName => "SSRule03";
    public override bool DoubleDeep => false;
    public override int Order => 300;

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
WHERE loc1.Tag = 1
AND loc2.Tag = 2
AND loc1.CellId = loc2.CellId
AND rack1.LanewayId = @lanewayId
AND loc2.xExists = 1
AND loc2.UnitloadCount = 0
AND loc2.OutboundCount = 0
AND loc2.InboundCount = 0
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
        p.Add("weight", storageInfo.Weight);
        p.Add("height", storageInfo.Height);
        p.Add("storageGroup", storageInfo.StorageGroup);
        p.Add("subStorageGroup", storageInfo.SubStorageGroup);
        p.Add("locSpec", storageInfo.ContainerSpecification);
        return p;
    }
}
