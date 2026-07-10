using System.Text;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Services;
using Wms.Core.Domain.Entities;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Services.ReportProviders;

/// <summary>
/// 可出库库存报表 Provider
/// 搜索条件：到位时间(单个日期)，仓库，批次，当前工艺
/// 获取数据字典 PROCESSTIME 所有子项，Unitloads.CurrentOperation 匹配子项 Name，
/// 计算到位时间和 Unitloads.CurrentLocationTime 时间差是否小于数据字典 Value(分钟)
/// 以批次为列表，字段包括：物料编码、物料名称、批次、数量、托盘数量
/// </summary>
public class AvailableStockProvider : IReportQueryProvider
{
    public string ReportCode => "available-stock";

    private readonly WmsDbContext _db;

    public AvailableStockProvider(WmsDbContext db) => _db = db;

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
        var operations = _db.Set<BasicDictionary>()
            .Where(d => _db.Set<BasicDictionary>().Any(p => p.No == "PROCESSTIME" && p.Id == d.ParentId)
                && d.Name != null)
            .Select(d => new FilterOption(d.Name!, d.Name!))
            .ToList();

        var warehouses = _db.Warehouses
            .OrderBy(w => w.Id)
            .Select(w => new FilterOption(w.xName!, w.Id.ToString()!))
            .ToList();

        return new Dictionary<string, List<FilterOption>>
        {
            ["currentOperation"] = operations,
            ["warehouseId"] = warehouses,
        };
    }

    private static string BuildSelect(List<string> columns)
    {
        if (columns == null || columns.Count == 0)
        {
            return @"agg.MaterialCode, agg.MaterialName, agg.Batch, agg.Quantity, agg.TrayCount";
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MaterialCode"] = "agg.MaterialCode",
            ["MaterialName"] = "agg.MaterialName",
            ["Batch"] = "agg.Batch",
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

    private string BuildWhere(Dictionary<string, object?> filters, Dictionary<string, object> sqlParams)
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

        if (filters.TryGetValue("currentOperation", out var op) && !string.IsNullOrEmpty(ReportFilterHelper.UnwrapValue(op)?.ToString()))
        {
            conditions.Add("u.CurrentOperation = @CurrentOperation");
            sqlParams["@CurrentOperation"] = ReportFilterHelper.UnwrapValue(op);
        }

        if (filters.TryGetValue("arrivalDate", out var ad) && ad != null)
        {
            var operation = ReportFilterHelper.UnwrapValue(op)?.ToString();
            int minutes = 0;
            if (!string.IsNullOrEmpty(operation))
            {
                var processTime = _db.Set<BasicDictionary>()
                    .FirstOrDefault(d => _db.Set<BasicDictionary>().Any(p => p.No == "PROCESSTIME" && p.Id == d.ParentId)
                        && d.Name == operation);
                if (processTime != null) int.TryParse(processTime.Value, out minutes);
            }

            var cutoffTime = ((DateTime)ReportFilterHelper.UnwrapValue(ad)!).AddMinutes(-minutes);
            conditions.Add("u.CurrentLocationTime < @CutoffTime");
            sqlParams["@CutoffTime"] = cutoffTime;
        }

        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
    }
}
