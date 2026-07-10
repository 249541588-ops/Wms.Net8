using Wms.Core.Application.DTOs;

namespace Wms.Core.Application.Services;

/// <summary>
/// 报表服务接口
/// </summary>
public interface IReportService
{
    /// <summary>
    /// 获取所有可用报表列表
    /// </summary>
    Task<List<ReportConfigListDto>> GetReportListAsync();

    /// <summary>
    /// 获取报表配置详情（含列定义、筛选器定义）
    /// </summary>
    Task<ReportConfigDto?> GetReportConfigAsync(string reportCode);

    /// <summary>
    /// 查询报表数据（分页）
    /// </summary>
    Task<ReportQueryResult> QueryReportDataAsync(ReportQueryRequest request);

    /// <summary>
    /// 获取用户的列配置列表
    /// </summary>
    Task<List<UserReportConfigDto>> GetUserConfigsAsync(string reportCode, int userId);

    /// <summary>
    /// 保存用户列配置
    /// </summary>
    Task<UserReportConfigDto> SaveUserConfigAsync(string reportCode, int userId, string userName, SaveUserConfigRequest request);

    /// <summary>
    /// 删除用户列配置
    /// </summary>
    Task DeleteUserConfigAsync(int configId);

    /// <summary>
    /// 提交导出任务
    /// </summary>
    Task<ReportExportTaskDto> SubmitExportAsync(string reportCode, int userId, string userName, ReportExportRequest request);

    /// <summary>
    /// 获取用户的导出任务列表
    /// </summary>
    Task<List<ReportExportTaskDto>> GetExportTasksAsync(int userId);

    /// <summary>
    /// 创建自定义报表（管理员）
    /// </summary>
    Task<ReportConfigDto> CreateCustomReportAsync(int userId, string userName, CreateCustomReportRequest request);

    /// <summary>
    /// 更新自定义报表（管理员）
    /// </summary>
    Task<ReportConfigDto?> UpdateCustomReportAsync(int id, UpdateCustomReportRequest request);

    /// <summary>
    /// 删除自定义报表（管理员）
    /// </summary>
    Task DeleteCustomReportAsync(int id);

    /// <summary>
    /// 获取自定义报表详情（编辑用，含 SQL 模板和筛选映射）
    /// </summary>
    Task<CustomReportDetailDto?> GetCustomReportDetailAsync(int id);

    /// <summary>
    /// 验证自定义报表 SQL（试运行，返回前10行）
    /// </summary>
    Task<ReportQueryResult> ValidateCustomReportAsync(int id, Dictionary<string, object?>? filters = null);

    /// <summary>
    /// 预览自定义报表数据（前10行）
    /// </summary>
    Task<ReportQueryResult> PreviewCustomReportAsync(int id, Dictionary<string, object?> filters);
}
