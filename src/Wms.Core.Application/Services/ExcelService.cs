using ClosedXML.Excel;
using ExcelDataReader;
using System.Data;
using System.Globalization;
using Wms.Core.Application.DTOs;

namespace Wms.Core.Application.Services;

/// <summary>
/// Excel 服务实现
/// </summary>
public class ExcelService : IExcelService
{
    /// <summary>
    /// 从 Excel 文件读取物料数据
    /// </summary>
    public async Task<List<MaterialImportDto>> ReadMaterialsFromExcelAsync(Stream fileStream, string sheetName = "Sheet1")
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using var reader = ExcelReaderFactory.CreateReader(fileStream);
        using var dataset = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = (_) => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true,
                FilterRow = (rowReader) =>
                {
                    // 跳过空行
                    var hasData = false;
                    for (var i = 0; i < rowReader.FieldCount; i++)
                    {
                        try
                        {
                            var value = rowReader.GetString(i);
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                hasData = true;
                                break;
                            }
                        }
                        catch
                        {
                            // 跳过无法读取的字段
                        }
                    }
                    return hasData;
                }
            }
        });

        var table = dataset.Tables[sheetName];
        if (table == null)
        {
            throw new InvalidOperationException($"工作表 '{sheetName}' 不存在");
        }

        var materials = new List<MaterialImportDto>();

        for (int i = 0; i < table.Rows.Count; i++)
        {
            try
            {
                var row = table.Rows[i];
                var material = new MaterialImportDto
                {
                    RowNumber = i + 2, // Excel 行号（从1开始，加表头）
                    MaterialCode = GetString(row, "物料编码") ?? GetString(row, "MaterialCode") ?? GetString(row, "materialCode") ?? string.Empty,
                    Description = GetString(row, "说明") ?? GetString(row, "Description") ?? GetString(row, "description") ?? string.Empty,
                    MaterialType = GetString(row, "物料类型") ?? GetString(row, "MaterialType") ?? GetString(row, "materialType"),
                    Specification = GetString(row, "规格") ?? GetString(row, "Specification") ?? GetString(row, "specification"),
                    Uom = GetString(row, "单位") ?? GetString(row, "Uom") ?? GetString(row, "uom") ?? "PCS",
                    Enabled = GetBool(row, "是否启用") ?? GetBool(row, "Enabled") ?? true
                };

                // 验证必填字段
                if (string.IsNullOrWhiteSpace(material.MaterialCode))
                {
                    continue; // 跳过无效行
                }

                materials.Add(material);
            }
            catch (Exception ex)
            {
                // 记录错误但继续读取
                Console.WriteLine($"读取第 {i + 2} 行时出错: {ex.Message}");
            }
        }

        return await Task.FromResult(materials);
    }

    /// <summary>
    /// 将库存数据导出为 Excel
    /// </summary>
    public async Task<byte[]> ExportStockToExcelAsync(List<StockExportDto> stocks)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("库存数据");

        // 添加表头
        var headers = new[]
        {
            "物料编码", "物料说明", "批次", "库存状态", "数量", "可用数量", "单位", "位置编码"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            worksheet.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        // 添加数据
        var row = 2;
        foreach (var stock in stocks)
        {
            worksheet.Cell(row, 1).Value = stock.MaterialCode;
            worksheet.Cell(row, 2).Value = stock.MaterialDescription;
            worksheet.Cell(row, 3).Value = stock.Batch;
            worksheet.Cell(row, 4).Value = stock.StockStatus;
            worksheet.Cell(row, 5).Value = stock.Quantity;
            worksheet.Cell(row, 6).Value = stock.QuantityAvailable;
            worksheet.Cell(row, 7).Value = stock.Uom;
            worksheet.Cell(row, 8).Value = stock.LocationCode;
            row++;
        }

        // 自动调整列宽
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await Task.FromResult(stream.ToArray());
    }

    /// <summary>
    /// 生成 Excel 模板文件
    /// </summary>
    public byte[] GenerateMaterialImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("物料导入");

        // 添加表头
        var headers = new[]
        {
            "物料编码*", "说明*", "物料类型", "规格", "单位*", "是否启用"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            worksheet.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        // 添加示例数据
        worksheet.Cell(2, 1).Value = "MAT001";
        worksheet.Cell(2, 2).Value = "示例物料";
        worksheet.Cell(2, 3).Value = "原材料";
        worksheet.Cell(2, 4).Value = "标准规格";
        worksheet.Cell(2, 5).Value = "PCS";
        worksheet.Cell(2, 6).Value = true;

        // 添加说明
        worksheet.Cell(5, 1).Value = "填写说明：";
        worksheet.Cell(6, 1).Value = "1. 带 * 的字段为必填项";
        worksheet.Cell(7, 1).Value = "2. 物料编码必须唯一";
        worksheet.Cell(8, 1).Value = "3. 是否启用填写 true 或 false";
        worksheet.Cell(9, 1).Value = "4. 删除示例数据后再导入";

        // 自动调整列宽
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// 从 DataRow 中获取字符串值
    /// </summary>
    private static string? GetString(DataRow row, string columnName)
    {
        try
        {
            var index = row.Table.Columns.IndexOf(columnName);
            if (index >= 0 && !row.IsNull(index))
            {
                return row[index].ToString();
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// 从 DataRow 中获取布尔值
    /// </summary>
    private static bool? GetBool(DataRow row, string columnName)
    {
        try
        {
            var index = row.Table.Columns.IndexOf(columnName);
            if (index >= 0 && !row.IsNull(index))
            {
                var value = row[index].ToString();
                if (bool.TryParse(value, out var result))
                {
                    return result;
                }
            }
        }
        catch { }

        return null;
    }
}
