using System.Text;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Services;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Services.ReportProviders;

/// <summary>
/// 库存统计报表 Provider
/// 搜索条件：生产时间开始/结束，仓库
/// 以批次为列表，字段包括：序号、物料编码、物料名称、批次、数量、真电芯数量、假电芯数量、托盘数量
/// </summary>
public class StockStatisticsProvider : IReportQueryProvider
{
    public string ReportCode => "stock-statistics";

    private readonly WmsDbContext _db;

    public StockStatisticsProvider(WmsDbContext db) => _db = db;

    public string BuildDataSql(Dictionary<string, object?> filters, List<string> columns, out Dictionary<string, object> sqlParams)
    {
        sqlParams = new Dictionary<string, object>();
        var where = BuildWhere(filters, sqlParams);
        var select = BuildSelect(columns);

        return $"""
            SELECT {select}
            FROM (
                SELECT m.MaterialCode, m.Description AS MaterialName, ui.Batch,
                    SUM(ui.Quantity) AS Quantity,
                    SUM(ui.Quantity) - SUM(ui.FalseQuantity) AS RealCellQuantity,
                    SUM(ui.FalseQuantity) AS FalseCellQuantity,
                    COUNT(DISTINCT u.UnitloadId) AS TrayCount
                FROM UnitloadItems ui
                JOIN Materials m ON ui.MaterialId = m.MaterialId
                JOIN Unitloads u ON ui.UnitloadId = u.UnitloadId
                LEFT JOIN Locations l ON u.LocationId = l.LocationId
                {where}
                GROUP BY m.MaterialCode, m.Description, ui.Batch
            ) AS agg
            """;
    }

    public string BuildCountSql(Dictionary<string, object?> filters, out Dictionary<string, object> sqlParams)
    {
        sqlParams = new Dictionary<string, object>();
        var where = BuildWhere(filters, sqlParams);

        return $"""
            SELECT COUNT(DISTINCT CONCAT(ui.MaterialId, '|', COALESCE(ui.Batch,'')))
            FROM UnitloadItems ui
            JOIN Materials m ON ui.MaterialId = m.MaterialId
            JOIN Unitloads u ON ui.UnitloadId = u.UnitloadId
            LEFT JOIN Locations l ON u.LocationId = l.LocationId
            {where}
            """;
    }

    public Dictionary<string, List<FilterOption>> GetFilterOptions()
    {
        var warehouses = _db.Warehouses
            .OrderBy(w => w.Id)
            .Select(w => new FilterOption(w.xName!, w.Id.ToString()!))
            .ToList();

        return new Dictionary<string, List<FilterOption>>
        {
            ["warehouseId"] = warehouses,
        };
    }

    private static string BuildSelect(List<string> columns)
    {
        if (columns == null || columns.Count == 0)
        {
            return @"agg.MaterialCode, agg.MaterialName, agg.Batch, agg.Quantity, agg.RealCellQuantity, agg.FalseCellQuantity, agg.TrayCount";
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MaterialCode"] = "agg.MaterialCode",
            ["MaterialName"] = "agg.MaterialName",
            ["Batch"] = "agg.Batch",
            ["Quantity"] = "agg.Quantity",
            ["RealCellQuantity"] = "agg.RealCellQuantity",
            ["FalseCellQuantity"] = "agg.FalseCellQuantity",
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

        if (filters.TryGetValue("batch", out var batch) && !string.IsNullOrEmpty(ReportFilterHelper.UnwrapValue(batch)?.ToString()))
        {
            conditions.Add("ui.Batch = @Batch");
            sqlParams["@Batch"] = ReportFilterHelper.UnwrapValue(batch);
        }

        if (filters.TryGetValue("startDate", out var sd) && sd != null)
        {
            conditions.Add("ui.ProductionTime >= @StartDate");
            sqlParams["@StartDate"] = ReportFilterHelper.UnwrapValue(sd);
        }

        if (filters.TryGetValue("endDate", out var ed) && ed != null)
        {
            conditions.Add("ui.ProductionTime <= @EndDate");
            sqlParams["@EndDate"] = ReportFilterHelper.UnwrapValue(ed);
        }

        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
    }
}
