using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Services;
using Wms.Core.Domain.Common;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 统计报表 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IReportExportService _exportService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IReportService reportService,
        IReportExportService exportService,
        ILogger<ReportsController> logger)
    {
        _reportService = reportService;
        _exportService = exportService;
        _logger = logger;
    }

    #region 报表数据查询

    /// <summary>
    /// 获取可用报表列表
    /// </summary>
    [HttpGet]
    public async Task<Result> GetReportList()
    {
        try
        {
            var list = await _reportService.GetReportListAsync();
            return Result<object>.Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取报表列表失败");
            return Result.Fail("获取报表列表失败");
        }
    }

    /// <summary>
    /// 获取报表配置详情（含列定义、筛选器定义）
    /// </summary>
    [HttpGet("{reportCode}/config")]
    public async Task<Result> GetReportConfig(string reportCode)
    {
        try
        {
            var config = await _reportService.GetReportConfigAsync(reportCode);
            if (config == null)
                return Result.Fail("报表不存在", "404");

            return Result<object>.Success(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取报表配置失败: {ReportCode}", reportCode);
            return Result.Fail("获取报表配置失败");
        }
    }

    /// <summary>
    /// 查询报表数据（分页）
    /// </summary>
    [HttpPost("{reportCode}/data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Result> QueryReportData(string reportCode, [FromBody] ReportQueryRequest request)
    {
        try
        {
            request = request with { ReportCode = reportCode };
            var result = await _reportService.QueryReportDataAsync(request);
            return Result<object>.Success(result);
        }
        catch (ArgumentException ex)
        {
            // Q402：用户传入非法 sortField 等参数，返回 400 而非 500
            _logger.LogWarning(ex, "报表查询参数非法: {ReportCode} SortField={SortField}",
                reportCode, request.SortField);
            return Result.Fail(ex.Message, "400");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询报表数据失败: {ReportCode}", reportCode);
            return Result.Fail($"查询失败: {ex.Message}");
        }
    }

    #endregion

    #region 导出

    /// <summary>
    /// 提交导出任务（异步）
    /// </summary>
    [HttpPost("{reportCode}/export")]
    public async Task<Result> SubmitExport(string reportCode, [FromBody] ReportExportRequest request)
    {
        try
        {
            var userId = GetUserId();
            var userName = GetUserName();
            request = request with { ReportCode = reportCode };
            var task = await _reportService.SubmitExportAsync(reportCode, userId, userName, request);
            return Result<object>.Success(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提交导出任务失败: {ReportCode}", reportCode);
            return Result.Fail($"提交导出失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 查询我的导出任务列表
    /// </summary>
    [HttpGet("exports")]
    public async Task<Result> GetExportTasks()
    {
        try
        {
            var userId = GetUserId();
            var tasks = await _reportService.GetExportTasksAsync(userId);
            return Result<object>.Success(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取导出任务列表失败");
            return Result.Fail("获取导出任务列表失败");
        }
    }

    /// <summary>
    /// 下载已完成的导出文件
    /// </summary>
    [HttpGet("exports/{taskId}/download")]
    public IActionResult DownloadExport(string taskId)
    {
        try
        {
            var filePath = _exportService.GetFilePath(taskId);
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return NotFound("导出文件不存在或已过期");

            var fileName = Path.GetFileName(filePath);
            var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return PhysicalFile(filePath, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载导出文件失败: {TaskId}", taskId);
            return BadRequest("下载失败");
        }
    }

    /// <summary>
    /// 删除导出任务/文件
    /// </summary>
    [HttpDelete("exports/{taskId}")]
    public async Task<Result> DeleteExport(string taskId)
    {
        try
        {
            await _exportService.DeleteExportFileAsync(taskId);
            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除导出任务失败: {TaskId}", taskId);
            return Result.Fail("删除失败");
        }
    }

    #endregion

    #region 自定义报表管理（管理员）

    /// <summary>
    /// 新增自定义报表
    /// </summary>
    [HttpPost("custom")]
    [Authorize(Roles = "Admin")]
    public async Task<Result> CreateCustomReport([FromBody] CreateCustomReportRequest request)
    {
        try
        {
            var userId = GetUserId();
            var userName = GetUserName();
            var result = await _reportService.CreateCustomReportAsync(userId, userName, request);
            return Result<object>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建自定义报表失败");
            return Result.Fail($"创建失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新自定义报表
    /// </summary>
    [HttpPut("custom/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<Result> UpdateCustomReport(int id, [FromBody] UpdateCustomReportRequest request)
    {
        try
        {
            var result = await _reportService.UpdateCustomReportAsync(id, request);
            if (result == null)
                return Result.Fail("报表不存在或非自定义报表", "404");

            return Result<object>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新自定义报表失败: {Id}", id);
            return Result.Fail($"更新失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除自定义报表
    /// </summary>
    [HttpDelete("custom/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<Result> DeleteCustomReport(int id)
    {
        try
        {
            await _reportService.DeleteCustomReportAsync(id);
            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除自定义报表失败: {Id}", id);
            return Result.Fail("删除失败");
        }
    }

    /// <summary>
    /// 获取自定义报表详情（编辑用）
    /// </summary>
    [HttpGet("custom/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<Result> GetCustomReportDetail(int id)
    {
        try
        {
            var result = await _reportService.GetCustomReportDetailAsync(id);
            if (result == null)
                return Result.Fail("报表不存在或非自定义报表", "404");

            return Result<object>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取自定义报表详情失败: {Id}", id);
            return Result.Fail("获取详情失败");
        }
    }

    /// <summary>
    /// 验证自定义报表 SQL（试运行）
    /// </summary>
    [HttpPost("custom/{id:int}/validate")]
    [Authorize(Roles = "Admin")]
    public async Task<Result> ValidateCustomReport(int id, [FromBody] Dictionary<string, object?>? filters = null)
    {
        try
        {
            var result = await _reportService.ValidateCustomReportAsync(id, filters);
            return Result<object>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证自定义报表失败: {Id}", id);
            return Result.Fail($"验证失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 预览自定义报表数据（前10行）
    /// </summary>
    [HttpPost("custom/{id:int}/preview")]
    [Authorize(Roles = "Admin")]
    public async Task<Result> PreviewCustomReport(int id, [FromBody] Dictionary<string, object?> filters)
    {
        try
        {
            var result = await _reportService.PreviewCustomReportAsync(id, filters);
            return Result<object>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预览自定义报表失败: {Id}", id);
            return Result.Fail($"预览失败: {ex.Message}");
        }
    }

    #endregion

    #region 用户列配置

    /// <summary>
    /// 获取我的列配置列表
    /// </summary>
    [HttpGet("{reportCode}/my-configs")]
    public async Task<Result> GetUserConfigs(string reportCode)
    {
        try
        {
            var userId = GetUserId();
            var configs = await _reportService.GetUserConfigsAsync(reportCode, userId);
            return Result<object>.Success(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户配置失败: {ReportCode}", reportCode);
            return Result.Fail("获取配置失败");
        }
    }

    /// <summary>
    /// 保存列配置
    /// </summary>
    [HttpPost("{reportCode}/my-configs")]
    public async Task<Result> SaveUserConfig(string reportCode, [FromBody] SaveUserConfigRequest request)
    {
        try
        {
            var userId = GetUserId();
            var userName = GetUserName();
            var result = await _reportService.SaveUserConfigAsync(reportCode, userId, userName, request);
            return Result<object>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存用户配置失败: {ReportCode}", reportCode);
            return Result.Fail("保存配置失败");
        }
    }

    /// <summary>
    /// 删除列配置
    /// </summary>
    [HttpDelete("my-configs/{id:int}")]
    public async Task<Result> DeleteUserConfig(int id)
    {
        try
        {
            await _reportService.DeleteUserConfigAsync(id);
            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除用户配置失败: {Id}", id);
            return Result.Fail("删除失败");
        }
    }

    #endregion

    #region 辅助方法

    private int GetUserId()
    {
        var claim = User.FindFirst("UserId") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    private string GetUserName()
    {
        return User.Identity?.Name ?? User.FindFirst("UserName")?.Value ?? "Unknown";
    }

    #endregion
}
