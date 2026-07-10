using System.Text;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Services;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Services.ReportProviders;

/// <summary>
/// 出入库统计报表 Provider
/// 以库区为列表，字段包括：库区名称、电芯数量、托盘数量
/// 搜索条件：时间开始/结束，仓库，业务类型
/// </summary>
public class InOutStatisticsProvider : IReportQueryProvider
{
    public string ReportCode => "inout-statistics";

    private readonly WmsDbContext _db;

    public InOutStatisticsProvider(WmsDbContext db) => _db = db;

    public string BuildDataSql(Dictionary<string, object?> filters, List<string> columns, out Dictionary<string, object> sqlParams)
    {
        sqlParams = new Dictionary<string, object>();
        var where = BuildWhere(filters, sqlParams);
        var select = BuildSelect(columns);

        return $"""
            SELECT {select}
            FROM (
                SELECT wh.xName as AreaName,
                    SUM(f.Quantity) AS Quantity,
                    COUNT(DISTINCT f.ContainerCode) AS TrayCount
                FROM Flows f
                LEFT JOIN Locations l ON f.LocationId = l.LocationId
                INNER JOIN Racks r ON l.RackId = r.RackId
                INNER JOIN Laneways lw ON r.LanewayId = lw.LanewayId
                INNER JOIN Warehouses wh ON wh.Id = lw.WarehouseId
                {where}
                GROUP BY wh.xName
            ) AS agg
            """;
    }

    public string BuildCountSql(Dictionary<string, object?> filters, out Dictionary<string, object> sqlParams)
    {
        sqlParams = new Dictionary<string, object>();
        var where = BuildWhere(filters, sqlParams);

        return $"""
            SELECT COUNT(DISTINCT wh.xName)
            FROM Flows f
            LEFT JOIN Locations l ON f.LocationId = l.LocationId
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

        var bizTypes = new List<FilterOption>
        {
            new("入库", "入库"),
            new("出库", "出库"),
        };

        return new Dictionary<string, List<FilterOption>>
        {
            ["warehouseId"] = warehouses,
            ["bizType"] = bizTypes,
        };
    }

    private static string BuildSelect(List<string> columns)
    {
        if (columns == null || columns.Count == 0)
        {
            return @"agg.AreaName, agg.Quantity, agg.TrayCount";
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AreaName"] = "agg.AreaName",
            ["Quantity"] = "agg.Quantity",
            ["TrayCount"] = "agg.TrayCount",
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

        if (filters.TryGetValue("warehouseId", out var wh) && wh != null)
        {
            conditions.Add("l.WarehouseId = @WarehouseId");
            sqlParams["@WarehouseId"] = ReportFilterHelper.UnwrapValue(wh);
        }

        if (filters.TryGetValue("startDate", out var sd) && sd != null)
        {
            conditions.Add("f.CreatedTime >= @StartDate");
            sqlParams["@StartDate"] = ReportFilterHelper.UnwrapValue(sd);
        }

        if (filters.TryGetValue("endDate", out var ed) && ed != null)
        {
            conditions.Add("f.CreatedTime <= @EndDate");
            sqlParams["@EndDate"] = ReportFilterHelper.UnwrapValue(ed);
        }

        if (filters.TryGetValue("bizType", out var bt) && !string.IsNullOrEmpty(ReportFilterHelper.UnwrapValue(bt)?.ToString()))
        {
            var bizValue = ReportFilterHelper.UnwrapValue(bt)!.ToString()!;
            if (bizValue == "入库")
            {
                conditions.Add("f.BizType IN ('入库', '入库双叉')");
            }
            else
            {
                conditions.Add("f.BizType = @BizType");
                sqlParams["@BizType"] = bizValue;
            }
        }

        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
    }
}
