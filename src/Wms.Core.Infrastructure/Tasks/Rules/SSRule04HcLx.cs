using Microsoft.Extensions.Logging;
using System.Data;
using Dapper;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Tasks;

namespace Wms.Core.Infrastructure.Tasks.Rules;

/// <summary>
/// SSRule04HcLx — 单深巷道特殊规则（行列限制）
/// </summary>
public class SSRule04HcLx : LocationAllocationRuleBase
{
    public SSRule04HcLx(ILogger<SSRule04HcLx> logger) : base(logger) { }

    public override string RuleName => "SSRule04HcLx";
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
AND loc.xColumn > 0
{excludes}
ORDER BY loc.xLevel, loc.xColumn, loc.WeightLimit, loc.HeightLimit";
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
