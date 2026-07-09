using System.Text;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Services;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Services.ReportProviders;

/// <summary>
/// 分档库存报表 Provider — 按 xLevel 等级以批次为列表统计电芯库存
/// 搜索条件：仓库，xLevel 档位
/// </summary>
public class GradedStockProvider : IReportQueryProvider
{
    public string ReportCode => "graded-stock";

    private readonly WmsDbContext _db;

    public GradedStockProvider(WmsDbContext db) => _db = db;

    public string BuildDataSql(Dictionary<string, object?> filters, List<string> columns, out Dictionary<string, object> sqlParams)
    {
        sqlParams = new Dictionary<string, object>();
        var where = BuildWhere(filters, sqlParams);
        var select = BuildSelect(columns);

        return $"""
            SELECT {select}
            FROM (
                SELECT m.MaterialCode, m.Description AS MaterialName, uid.xLevel, ui.Batch,
                    COUNT(*) AS CellCount,
                    COUNT(DISTINCT u.UnitloadId) AS TrayCount
                FROM UnitloadItemDetails uid
                JOIN UnitloadItems ui ON uid.UnitloadItemId = ui.UnitloadItemId
                JOIN Unitloads u ON ui.UnitloadId = u.UnitloadId
                JOIN Materials m ON ui.MaterialId = m.MaterialId
                LEFT JOIN Locations l ON u.LocationId = l.LocationId
                {where}
                GROUP BY m.MaterialCode, m.Description, uid.xLevel, ui.Batch
            ) AS agg
            """;
    }

    public string BuildCountSql(Dictionary<string, object?> filters, out Dictionary<string, object> sqlParams)
    {
        sqlParams = new Dictionary<string, object>();
        var where = BuildWhere(filters, sqlParams);

        return $"""
            SELECT COUNT(DISTINCT CONCAT(ui.MaterialId, '|', COALESCE(uid.xLevel,''), '|', COALESCE(ui.Batch,'')))
            FROM UnitloadItemDetails uid
            JOIN UnitloadItems ui ON uid.UnitloadItemId = ui.UnitloadItemId
            JOIN Unitloads u ON ui.UnitloadId = u.UnitloadId
            JOIN Materials m ON ui.MaterialId = m.MaterialId
            LEFT JOIN Locations l ON u.LocationId = l.LocationId
            {where}
            """;
    }

    public Dictionary<string, List<FilterOption>> GetFilterOptions()
    {
        var xLevels = _db.Set<UnitloadItemDetail>()
            .Where(d => d.xLevel != null)
            .Select(d => d.xLevel!)
            .Distinct()
            .OrderBy(x => x)
            .Select(x => new FilterOption(x, x))
            .ToList();

        var warehouses = _db.Warehouses
            .OrderBy(w => w.Id)
            .Select(w => new FilterOption(w.xName!, w.Id.ToString()!))
            .ToList();

        return new Dictionary<string, List<FilterOption>>
        {
            ["xLevel"] = xLevels,
            ["warehouseId"] = warehouses,
        };
    }

    private static string BuildSelect(List<string> columns)
    {
        if (columns == null || columns.Count == 0)
        {
            return @"agg.MaterialCode, agg.MaterialName, agg.xLevel, agg.Batch, agg.CellCount, agg.TrayCount";
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MaterialCode"] = "agg.MaterialCode",
            ["MaterialName"] = "agg.MaterialName",
            ["xLevel"] = "agg.xLevel",
            ["Batch"] = "agg.Batch",
            ["CellCount"] = "agg.CellCount",
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

        if (filters.TryGetValue("xLevel", out var xl) && !string.IsNullOrEmpty(ReportFilterHelper.UnwrapValue(xl)?.ToString()))
        {
            conditions.Add("uid.xLevel = @XLevel");
            sqlParams["@XLevel"] = ReportFilterHelper.UnwrapValue(xl);
        }

        if (filters.TryGetValue("batch", out var batch) && !string.IsNullOrEmpty(ReportFilterHelper.UnwrapValue(batch)?.ToString()))
        {
            conditions.Add("ui.Batch = @Batch");
            sqlParams["@Batch"] = ReportFilterHelper.UnwrapValue(batch);
        }

        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
    }
}
