using Wms.Core.Application.DTOs;

namespace Wms.Core.Application.Services;

/// <summary>
/// 报表查询提供者接口 — 预置报表实现此接口
/// </summary>
public interface IReportQueryProvider
{
    /// <summary>
    /// 报表编码
    /// </summary>
    string ReportCode { get; }

    /// <summary>
    /// 构建数据查询 SQL（不含分页和排序）
    /// </summary>
    string BuildDataSql(Dictionary<string, object?> filters, List<string> columns, out Dictionary<string, object> sqlParams);

    /// <summary>
    /// 构建 COUNT SQL
    /// </summary>
    string BuildCountSql(Dictionary<string, object?> filters, out Dictionary<string, object> sqlParams);

    /// <summary>
    /// 返回筛选条件下拉选项数据
    /// </summary>
    Dictionary<string, List<FilterOption>> GetFilterOptions();
}
