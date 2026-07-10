using ClosedXML.Excel;
using ExcelDataReader;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Domain.Common;
using Wms.Core.WebApi.Helpers;
using System.Data;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 文件上传/导入/导出 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly ILogger<UploadController> _logger;
    private readonly string _uploadBasePath;

    public UploadController(
        ILogger<UploadController> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var configPath = configuration["Upload:BasePath"];
        _uploadBasePath = string.IsNullOrWhiteSpace(configPath) ? Path.Combine(Directory.GetCurrentDirectory(), "uploads") : configPath;
        FileHelper.EnsureDirectory(_uploadBasePath);
    }

    #region 图片上传

    /// <summary>
    /// 图片上传（按模块分目录）
    /// </summary>
    /// <param name="file">图片文件</param>
    /// <param name="module">模块名（如 mater、battery、gear）</param>
    /// <returns>文件访问 URL</returns>
    [HttpPost("image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5MB
    public async Task<Result> UploadImage(IFormFile file, string module = "default")
    {
        try
        {
            if (file == null || file.Length == 0)
                return Result.Fail("请选择文件");

            if (!FileHelper.IsAllowedImageExtension(file.FileName))
                return Result.Fail("不支持的图片格式，允许: .jpg, .jpeg, .png, .gif, .bmp, .webp");

            // 保存到 uploads/images/{module}/{yyyyMM}/
            var dir = FileHelper.GetModulePath(Path.Combine(_uploadBasePath, "images"), module);
            var fileName = FileHelper.GenerateFileName(file.FileName);
            var filePath = Path.Combine(dir, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = filePath.Replace(Directory.GetCurrentDirectory(), "").Replace("\\", "/");
            if (!url.StartsWith("/")) url = "/" + url;

            _logger.LogInformation("图片上传成功: {Url}", url);
            return Result<object>.Success(new { url, fileName = file.FileName }, "上传成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图片上传失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    #endregion

    #region Excel 导入

    /// <summary>
    /// Excel 导入（按模块分目录存档，通用解析返回数据）
    /// </summary>
    /// <param name="file">Excel 文件</param>
    /// <param name="module">模块名（如 mater、workprocedure）</param>
    /// <returns>解析后的表头和数据行</returns>
    [HttpPost("excel-import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<Result> ExcelImport(IFormFile file, string module = "default")
    {
        try
        {
            if (file == null || file.Length == 0)
                return Result.Fail("请选择文件");

            if (!FileHelper.IsAllowedExcelExtension(file.FileName))
                return Result.Fail("不支持的文件格式，允许: .xlsx, .xls, .csv");

            // 存档到 uploads/excel/import/{module}/{yyyyMM}/
            var dir = FileHelper.GetModulePath(Path.Combine(_uploadBasePath, "excel", "import"), module);
            var savedName = FileHelper.GenerateFileName(file.FileName);
            var filePath = Path.Combine(dir, savedName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 读取并解析
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            await using var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(readStream);
            using var dataset = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true,
                    FilterRow = rowReader =>
                    {
                        for (var i = 0; i < rowReader.FieldCount; i++)
                        {
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(rowReader.GetString(i)))
                                    return true;
                            }
                            catch { }
                        }
                        return false;
                    }
                }
            });

            var table = dataset.Tables[0];
            if (table == null || table.Rows.Count == 0)
                return Result.Fail("Excel 文件为空或无有效数据");

            // 提取表头
            var headers = new List<string>();
            foreach (DataColumn col in table.Columns)
            {
                headers.Add(col.ColumnName);
            }

            // 提取数据行
            var rows = new List<List<string?>>();
            foreach (DataRow row in table.Rows)
            {
                var cells = new List<string?>();
                foreach (DataColumn col in table.Columns)
                {
                    cells.Add(row[col]?.ToString());
                }
                rows.Add(cells);
            }

            var relativePath = filePath.Replace(Directory.GetCurrentDirectory(), "").Replace("\\", "/");
            if (!relativePath.StartsWith("/")) relativePath = "/" + relativePath;

            _logger.LogInformation("Excel 导入成功: {Path}, 共 {Count} 行", relativePath, rows.Count);
            return Result<object>.Success(new
            {
                headers,
                rows,
                totalCount = rows.Count,
                filePath = relativePath
            }, "导入解析成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel 导入失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    #endregion

    #region Excel 导出

    /// <summary>
    /// Excel 导出（通用方法，前端传表头和数据行）
    /// </summary>
    /// <param name="request">导出请求</param>
    /// <returns>xlsx 文件</returns>
    [HttpPost("excel-export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public IActionResult ExcelExport([FromBody] ExcelExportRequest request)
    {
        try
        {
            if (request.Headers == null || request.Headers.Count == 0)
                return BadRequest(Result.Fail("表头不能为空"));

            using var workbook = new XLWorkbook();
            var sheetName = string.IsNullOrWhiteSpace(request.SheetName) ? "Sheet1" : request.SheetName;
            var worksheet = workbook.Worksheets.Add(sheetName);

            // 写入表头（蓝底白字加粗）
            for (int i = 0; i < request.Headers.Count; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = request.Headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                cell.Style.Font.FontColor = XLColor.White;
            }

            // 写入数据行
            if (request.Rows != null)
            {
                for (int r = 0; r < request.Rows.Count; r++)
                {
                    var row = request.Rows[r];
                    for (int c = 0; c < row.Count; c++)
                    {
                        worksheet.Cell(r + 2, c + 1).Value = row[c] ?? "";
                    }
                }
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            var fileName = string.IsNullOrWhiteSpace(request.FileName) ? "export" : request.FileName;
            if (!fileName.EndsWith(".xlsx")) fileName += ".xlsx";

            _logger.LogInformation("Excel 导出成功: {FileName}, 共 {Count} 行", fileName, request.Rows?.Count ?? 0);

            return File(content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel 导出失败: {Message}", ex.Message);
            return BadRequest(Result.Fail("操作失败"));
        }
    }

    #endregion
}

/// <summary>
/// Excel 导出请求
/// </summary>
public class ExcelExportRequest
{
    /// <summary>
    /// 文件名（不含扩展名）
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 工作表名称
    /// </summary>
    public string? SheetName { get; set; }

    /// <summary>
    /// 表头列表
    /// </summary>
    public List<string> Headers { get; set; } = new();

    /// <summary>
    /// 数据行列表
    /// </summary>
    public List<List<string?>> Rows { get; set; } = new();
}
