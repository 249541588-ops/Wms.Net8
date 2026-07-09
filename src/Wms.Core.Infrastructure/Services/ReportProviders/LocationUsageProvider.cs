using System.Text;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Services;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Services.ReportProviders;

/// <summary>
/// 库位使用率报表 Provider
/// 以巷道为列表，列名包括：序号、巷道编码【库区名称】、货位总数、已用货位、可用货位
/// </summary>
public class LocationUsageProvider : IReportQueryProvider
{
    private readonly WmsDbContext _db;

    public LocationUsageProvider(WmsDbContext db) => _db = db;

    public string ReportCode => "location-usage";

    public string BuildDataSql(Dictionary<string, object?> filters, List<string> columns, out Dictionary<string, object> sqlParams)
    {
        sqlParams = new Dictionary<string, object>();
        var where = BuildWhere(filters, sqlParams);
        var select = BuildSelect(columns);

        return $"""
            SELECT {select}
            FROM (
                SELECT
                    ROW_NUMBER() OVER (ORDER BY lw.LanewayId) AS RowNumber,
                    lw.LanewayCode + '【' + ISNULL(wh.xName, '') + '】' AS LanewayDisplay,
                    COUNT(*) AS TotalSlots,
                    SUM(CASE WHEN l.InboundCount > 0 THEN 1 ELSE 0 END) AS UsedSlots,
                    COUNT(*) - SUM(CASE WHEN l.InboundCount > 0 THEN 1 ELSE 0 END) AS AvailableSlots
                FROM Locations l
                INNER JOIN Racks r ON l.RackId = r.RackId
                INNER JOIN Laneways lw ON r.LanewayId = lw.LanewayId
                INNER JOIN Warehouses wh ON wh.Id = lw.WarehouseId
                {where}
                GROUP BY lw.LanewayId, lw.LanewayCode, wh.xName
            ) AS sub
            ORDER BY sub.LanewayDisplay ASC
            """;
    }

    public string BuildCountSql(Dictionary<string, object?> filters, out Dictionary<string, object> sqlParams)
    {
        sqlParams = new Dictionary<string, object>();
        var where = BuildWhere(filters, sqlParams);

        return $"""
            SELECT COUNT(DISTINCT lw.LanewayId)
            FROM Locations l
            INNER JOIN Racks r ON l.RackId = r.RackId
            INNER JOIN Laneways lw ON r.LanewayId = lw.LanewayId
            INNER JOIN Warehouses wh ON wh.Id = lw.WarehouseId
            {where}
            """;
    }

    public Dictionary<string, List<FilterOption>> GetFilterOptions()
    {
        var warehouses = _db.Warehouses
            .OrderBy(w => w.Id)
            .Select(w => new FilterOption(w.xName!, w.Id.ToString()!))
            .ToList();

        // var areas = _db.Laneways
        //     .Where(lw => lw.Area != null)
        //     .Select(lw => lw.Area!)
        //     .Distinct()
        //     .OrderBy(a => a)
        //     .Select(a => new FilterOption(a, a))
        //     .ToList();

        return new Dictionary<string, List<FilterOption>>
        {
            ["warehouseId"] = warehouses,
            //["area"] = areas,
        };
    }

    private static string BuildSelect(List<string> columns)
    {
        if (columns == null || columns.Count == 0)
        {
            return "sub.RowNumber, sub.LanewayDisplay, sub.TotalSlots, sub.UsedSlots, sub.AvailableSlots";
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RowNumber"] = "sub.RowNumber",
            ["LanewayDisplay"] = "sub.LanewayDisplay",
            ["TotalSlots"] = "sub.TotalSlots",
            ["UsedSlots"] = "sub.UsedSlots",
            ["AvailableSlots"] = "sub.AvailableSlots",
            ["UsageRate"] = "CASE WHEN sub.TotalSlots > 0 THEN CAST(sub.UsedSlots AS float) / sub.TotalSlots * 100 ELSE 0 END AS UsageRate",
        };

        var sb = new StringBuilder();
        foreach (var col in columns)
        {
            if (map.TryGetValue(col, out var sql))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(sql);
            }
        }
        return sb.Length > 0 ? sb.ToString() : "*";
    }

    private static string BuildWhere(Dictionary<string, object?> filters, Dictionary<string, object> sqlParams)
    {
        var conditions = new List<string>();

        if (filters.TryGetValue("warehouseId", out var wid) && wid != null)
        {
            conditions.Add("l.WarehouseId = @WarehouseId");
            sqlParams["@WarehouseId"] = ReportFilterHelper.UnwrapValue(wid) ?? DBNull.Value;
        }

        if (filters.TryGetValue("area", out var area) && !string.IsNullOrEmpty(ReportFilterHelper.UnwrapValue(area)?.ToString()))
        {
            conditions.Add("wh.xName = @Area");
            sqlParams["@Area"] = ReportFilterHelper.UnwrapValue(area)!.ToString()!;
        }

        if (filters.TryGetValue("lanewayCode", out var lc) && !string.IsNullOrEmpty(ReportFilterHelper.UnwrapValue(lc)?.ToString()))
        {
            conditions.Add("lw.LanewayCode = @LanewayCode");
            sqlParams["@LanewayCode"] = ReportFilterHelper.UnwrapValue(lc)!.ToString()!;
        }

        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
    }
}
