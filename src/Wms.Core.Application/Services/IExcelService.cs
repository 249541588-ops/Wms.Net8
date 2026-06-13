using ExcelDataReader;
using System.Data;
using System.Text;
using Wms.Core.Application.DTOs;

namespace Wms.Core.Application.Services;

/// <summary>
/// Excel 服务接口
/// </summary>
public interface IExcelService
{
    /// <summary>
    /// 从 Excel 文件读取物料数据
    /// </summary>
    Task<List<MaterialImportDto>> ReadMaterialsFromExcelAsync(Stream fileStream, string sheetName = "Sheet1");

    /// <summary>
    /// 将库存数据导出为 Excel
    /// </summary>
    Task<byte[]> ExportStockToExcelAsync(List<StockExportDto> stocks);

    /// <summary>
    /// 生成 Excel 模板文件
    /// </summary>
    byte[] GenerateMaterialImportTemplate();
}
