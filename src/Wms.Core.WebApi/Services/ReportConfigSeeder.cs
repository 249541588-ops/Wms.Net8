using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 报表配置种子数据
/// </summary>
public static class ReportConfigSeeder
{
    /// <summary>
    /// 种子预置报表配置（幂等：按 ReportCode 判断是否已存在）
    /// </summary>
    public static async Task SeedAsync(WmsDbContext db, ILogger logger)
    {
        var reports = BuildSeedData();

        foreach (var report in reports)
        {
            var existing = await db.ReportConfigs
                .FirstOrDefaultAsync(r => r.ReportCode == report.ReportCode);

            if (existing == null)
            {
                db.ReportConfigs.Add(report);
                logger.LogInformation("种子报表配置: {Code} - {Name}", report.ReportCode, report.ReportName);
            }
            else
            {
                existing.ReportName = report.ReportName;
                existing.Category = report.Category;
                existing.Description = report.Description;
                existing.ReportType = report.ReportType;
                existing.DefaultColumns = report.DefaultColumns;
                existing.AvailableColumns = report.AvailableColumns;
                existing.AvailableFilters = report.AvailableFilters;
                existing.DefaultSort = report.DefaultSort;
                existing.IsEnabled = report.IsEnabled;
                existing.ModifiedTime = DateTime.Now;
                logger.LogInformation("更新报表配置: {Code} - {Name}", report.ReportCode, report.ReportName);
            }
        }

        await db.SaveChangesAsync();
    }

    private static List<ReportConfig> BuildSeedData()
    {
        return
        [
            BuildStockStatistics(),
            BuildInOutStatistics(),
            BuildAvailableStock(),
            BuildLocationUsage(),
            BuildGradedStock(),
        ];
    }

    private static ReportConfig BuildStockStatistics()
    {
        var columns = new[]
        {
            new { Field = "MaterialCode", Title = "物料编码", DataType = "string", Sortable = true },
            new { Field = "MaterialName", Title = "物料名称", DataType = "string", Sortable = true },
            new { Field = "Batch", Title = "批次", DataType = "string", Sortable = true },
            new { Field = "Quantity", Title = "数量", DataType = "number", Sortable = true },
            new { Field = "RealCellQuantity", Title = "真电芯数量", DataType = "number", Sortable = true },
            new { Field = "FalseCellQuantity", Title = "假电芯数量", DataType = "number", Sortable = true },
            new { Field = "TrayCount", Title = "托盘数量", DataType = "number", Sortable = true },
        };

        var filters = new[]
        {
            new { Field = "warehouseId", Title = "仓库", Type = "select", Options = (object?)null, Required = false },
            new { Field = "batch", Title = "批次", Type = "text", Options = (object?)null, Required = false },
            new { Field = "startDate", Title = "生产时间起", Type = "date", Options = (object?)null, Required = false },
            new { Field = "endDate", Title = "生产时间止", Type = "date", Options = (object?)null, Required = false },
        };

        return new ReportConfig
        {
            ReportCode = "stock-statistics",
            ReportName = "库存统计报表",
            Category = "Inventory",
            Description = "以批次为列表，展示物料编码、名称、批次、数量、真电芯数量、假电芯数量、托盘数量",
            ReportType = "System",
            DefaultColumns = JsonSerializer.Serialize(new[] { "MaterialCode", "MaterialName", "Batch", "Quantity", "RealCellQuantity", "FalseCellQuantity", "TrayCount" }),
            AvailableColumns = JsonSerializer.Serialize(columns),
            AvailableFilters = JsonSerializer.Serialize(filters),
            DefaultSort = "MaterialCode ASC",
            IsEnabled = true,
            CreatedTime = DateTime.Now,
        };
    }

    private static ReportConfig BuildInOutStatistics()
    {
        var columns = new[]
        {
            new { Field = "AreaName", Title = "库区名称", DataType = "string", Sortable = true },
            new { Field = "Quantity", Title = "电芯数量", DataType = "number", Sortable = true },
            new { Field = "TrayCount", Title = "托盘数量", DataType = "number", Sortable = true },
        };

        var filters = new[]
        {
            new { Field = "warehouseId", Title = "仓库", Type = "select", Options = (object?)null, Required = false },
            new { Field = "startDate", Title = "开始时间", Type = "date", Options = (object?)null, Required = false },
            new { Field = "endDate", Title = "结束时间", Type = "date", Options = (object?)null, Required = false },
            new { Field = "bizType", Title = "业务类型", Type = "select", Options = (object?)null, Required = false },
        };

        return new ReportConfig
        {
            ReportCode = "inout-statistics",
            ReportName = "出入库统计报表",
            Category = "InOut",
            Description = "以库区为列表，展示电芯数量和托盘数量",
            ReportType = "System",
            DefaultColumns = JsonSerializer.Serialize(new[] { "AreaName", "Quantity", "TrayCount" }),
            AvailableColumns = JsonSerializer.Serialize(columns),
            AvailableFilters = JsonSerializer.Serialize(filters),
            DefaultSort = "AreaName ASC",
            IsEnabled = true,
            CreatedTime = DateTime.Now,
        };
    }

    private static ReportConfig BuildAvailableStock()
    {
        var columns = new[]
        {
            new { Field = "MaterialCode", Title = "物料编码", DataType = "string", Sortable = true },
            new { Field = "MaterialName", Title = "物料名称", DataType = "string", Sortable = true },
            new { Field = "Batch", Title = "批次", DataType = "string", Sortable = true },
            new { Field = "Quantity", Title = "数量", DataType = "number", Sortable = true },
            new { Field = "TrayCount", Title = "托盘数量", DataType = "number", Sortable = true },
        };

        var filters = new[]
        {
            new { Field = "warehouseId", Title = "仓库", Type = "select", Options = (object?)null, Required = false },
            new { Field = "batch", Title = "批次", Type = "text", Options = (object?)null, Required = false },
            new { Field = "currentOperation", Title = "当前工艺", Type = "select", Options = (object?)null, Required = false },
            new { Field = "arrivalDate", Title = "到位时间", Type = "date", Options = (object?)null, Required = false },
        };

        return new ReportConfig
        {
            ReportCode = "available-stock",
            ReportName = "可出库库存报表",
            Category = "Inventory",
            Description = "以批次为列表，展示到位时间和工艺匹配的可出库库存，含数量和托盘数量",
            ReportType = "System",
            DefaultColumns = JsonSerializer.Serialize(new[] { "MaterialCode", "MaterialName", "Batch", "Quantity", "TrayCount" }),
            AvailableColumns = JsonSerializer.Serialize(columns),
            AvailableFilters = JsonSerializer.Serialize(filters),
            DefaultSort = "MaterialCode ASC",
            IsEnabled = true,
            CreatedTime = DateTime.Now,
        };
    }

    private static ReportConfig BuildLocationUsage()
    {
        var columns = new[]
        {
            new { Field = "RowNumber", Title = "序号", DataType = "number", Sortable = false },
            new { Field = "LanewayDisplay", Title = "巷道编码【库区名称】", DataType = "string", Sortable = true },
            new { Field = "TotalSlots", Title = "货位总数", DataType = "number", Sortable = true },
            new { Field = "UsedSlots", Title = "已用货位", DataType = "number", Sortable = true },
            new { Field = "AvailableSlots", Title = "可用货位", DataType = "number", Sortable = true },
            new { Field = "UsageRate", Title = "使用率(%)", DataType = "number", Sortable = true },
        };

        var filters = new[]
        {
            new { Field = "warehouseId", Title = "仓库", Type = "select", Options = (object?)null, Required = false },
            //new { Field = "area", Title = "库区", Type = "select", Options = (object?)null, Required = false },
            new { Field = "lanewayCode", Title = "巷道编码", Type = "text", Options = (object?)null, Required = false },
        };

        return new ReportConfig
        {
            ReportCode = "location-usage",
            ReportName = "库位使用率报表",
            Category = "Location",
            Description = "以巷道为列表，展示货位总数、已用货位、可用货位统计",
            ReportType = "System",
            DefaultColumns = JsonSerializer.Serialize(new[] { "RowNumber", "LanewayDisplay", "TotalSlots", "UsedSlots", "AvailableSlots", "UsageRate" }),
            AvailableColumns = JsonSerializer.Serialize(columns),
            AvailableFilters = JsonSerializer.Serialize(filters),
            DefaultSort = "",
            IsEnabled = true,
            CreatedTime = DateTime.Now,
        };
    }

    private static ReportConfig BuildGradedStock()
    {
        var columns = new[]
        {
            new { Field = "MaterialCode", Title = "物料编码", DataType = "string", Sortable = true },
            new { Field = "MaterialName", Title = "物料名称", DataType = "string", Sortable = true },
            new { Field = "xLevel", Title = "等级", DataType = "string", Sortable = true },
            new { Field = "Batch", Title = "批次", DataType = "string", Sortable = true },
            new { Field = "CellCount", Title = "电芯数量", DataType = "number", Sortable = true },
            new { Field = "TrayCount", Title = "托盘数量", DataType = "number", Sortable = true },
        };

        var filters = new[]
        {
            new { Field = "warehouseId", Title = "仓库", Type = "select", Options = (object?)null, Required = false },
            new { Field = "xLevel", Title = "等级", Type = "select", Options = (object?)null, Required = false },
            new { Field = "batch", Title = "批次", Type = "text", Options = (object?)null, Required = false },
        };

        return new ReportConfig
        {
            ReportCode = "graded-stock",
            ReportName = "分档库存报表",
            Category = "Inventory",
            Description = "按电芯等级(xLevel)以批次为列表，展示电芯数量和托盘数量",
            ReportType = "System",
            DefaultColumns = JsonSerializer.Serialize(new[] { "MaterialCode", "MaterialName", "xLevel", "Batch", "CellCount", "TrayCount" }),
            AvailableColumns = JsonSerializer.Serialize(columns),
            AvailableFilters = JsonSerializer.Serialize(filters),
            DefaultSort = "MaterialCode ASC, xLevel ASC",
            IsEnabled = true,
            CreatedTime = DateTime.Now,
        };
    }
}
