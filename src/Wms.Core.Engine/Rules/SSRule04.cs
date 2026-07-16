using Microsoft.Extensions.Logging;
using System.Data;
using Dapper;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Tasks;

namespace Wms.Core.Infrastructure.Tasks.Rules;

/// <summary>
/// SSRule04 — 单深巷道，一头进一头出
/// </summary>
public class SSRule04 : LocationAllocationRuleBase
{
    public SSRule04(ILogger<SSRule04> logger) : base(logger) { }

    public override string RuleName => "SSRule04";
    public override bool DoubleDeep => false;
    public override int Order => 100;

    protected override string BuildSql(
        int lanewayId, UnitloadStorageInfo storageInfo,
        int[] excludedIds, int[] excludedColumns, int[] excludedLevels,
        string orderBy, string sortMethod)
    {
        var excludes = BuildExcludeClauses(excludedIds, excludedColumns, excludedLevels);
        return $@"
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
AND loc.xSpecification = @locSpec
{excludes}
ORDER BY loc.xLevel, loc.WeightLimit, loc.HeightLimit, {SafeOrderBy(orderBy, sortMethod)}";
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
