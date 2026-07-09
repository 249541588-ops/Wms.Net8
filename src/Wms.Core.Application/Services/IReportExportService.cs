namespace Wms.Core.Application.Services;

/// <summary>
/// 报表导出服务接口
/// </summary>
public interface IReportExportService
{
    /// <summary>
    /// 执行导出（由 Hangfire 后台调用）
    /// </summary>
    Task ExecuteExportAsync(string taskId, string reportCode, Dictionary<string, object?> filters, List<string>? columns, string filePath);

    /// <summary>
    /// 获取导出文件下载路径
    /// </summary>
    string GetFilePath(string taskId);

    /// <summary>
    /// 删除导出文件
    /// </summary>
    Task DeleteExportFileAsync(string taskId);
}
