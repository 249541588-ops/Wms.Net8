namespace Wms.Core.Application.DTOs;

/// <summary>
/// 报表列定义
/// </summary>
public record ReportColumnDefinition(string Field, string Title, string DataType = "string", bool Sortable = true, int? Width = null);

/// <summary>
/// 报表筛选器定义
/// </summary>
public record ReportFilterDefinition(string Field, string Title, string Type = "text", List<FilterOption>? Options = null, bool Required = false);

/// <summary>
/// 筛选器选项
/// </summary>
public record FilterOption(string Label, string Value);

/// <summary>
/// 报表查询请求
/// </summary>
public record ReportQueryRequest(
    string? ReportCode = null,
    Dictionary<string, object?>? Filters = null,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortField = null,
    string? SortDirection = null
)
{
    public Dictionary<string, object?> Filters { get; init; } = Filters ?? new();
}

/// <summary>
/// 报表查询结果（动态列）
/// </summary>
public record ReportQueryResult(
    int TotalCount,
    int PageNumber,
    int PageSize,
    List<string> Columns,
    List<Dictionary<string, object?>> Data
);

/// <summary>
/// 报表配置 DTO
/// </summary>
public record ReportConfigDto(
    int Id,
    string ReportCode,
    string ReportName,
    string? Category,
    string? Description,
    string? ReportType,
    List<string> DefaultColumns,
    List<ReportColumnDefinition> AvailableColumns,
    List<ReportFilterDefinition> AvailableFilters,
    string? DefaultSort,
    bool IsEnabled
);

/// <summary>
/// 自定义报表详情 DTO（编辑用，含 SQL 模板和筛选映射）
/// </summary>
public record CustomReportDetailDto(
    int Id,
    string ReportCode,
    string ReportName,
    string? Category,
    string? Description,
    string? ReportType,
    List<string> DefaultColumns,
    List<ReportColumnDefinition> AvailableColumns,
    List<ReportFilterDefinition> AvailableFilters,
    string? DefaultSort,
    bool IsEnabled,
    string? SqlTemplate,
    string? CountSqlTemplate,
    List<FilterSqlMappingItem>? FilterSqlMappings
);

/// <summary>
/// 报表列表 DTO（精简版）
/// </summary>
public record ReportConfigListDto(
    int Id,
    string ReportCode,
    string ReportName,
    string? Category,
    string? Description,
    string? ReportType
);

/// <summary>
/// 用户列配置 DTO
/// </summary>
public record UserReportConfigDto(
    int Id,
    string ConfigName,
    List<string> SelectedColumns,
    List<string> ColumnOrder,
    Dictionary<string, int>? ColumnWidths,
    bool IsDefault
);

/// <summary>
/// 保存用户配置请求
/// </summary>
public record SaveUserConfigRequest(
    string ConfigName,
    List<string> SelectedColumns,
    List<string> ColumnOrder,
    Dictionary<string, int>? ColumnWidths,
    bool IsDefault = false
);

/// <summary>
/// 导出请求
/// </summary>
public record ReportExportRequest(
    string? ReportCode = null,
    Dictionary<string, object?>? Filters = null,
    List<string>? Columns = null
)
{
    public Dictionary<string, object?> Filters { get; init; } = Filters ?? new();
}

/// <summary>
/// 导出任务 DTO
/// </summary>
public record ReportExportTaskDto(
    string TaskId,
    string ReportCode,
    string Status,
    string? FileName,
    long? FileSize,
    int? TotalRows,
    DateTime? CompletedAt,
    string? ErrorMessage
);

/// <summary>
/// 创建自定义报表请求
/// </summary>
public record CreateCustomReportRequest(
    string ReportCode,
    string ReportName,
    string? Category,
    string? Description,
    List<ReportColumnDefinition> AvailableColumns,
    List<ReportFilterDefinition> AvailableFilters,
    List<string> DefaultColumns,
    string? DefaultSort,
    string SqlTemplate,
    string CountSqlTemplate,
    List<FilterSqlMappingItem> FilterSqlMappings
);

/// <summary>
/// 筛选条件到 SQL WHERE 片段的映射项
/// </summary>
public record FilterSqlMappingItem(
    string FilterField,
    string SqlExpression  // 如 "MaterialCode LIKE @MaterialCode" 或 "CreatedTime >= @StartDate"
);

/// <summary>
/// 更新自定义报表请求
/// </summary>
public record UpdateCustomReportRequest(
    string? ReportName,
    string? Category,
    string? Description,
    List<ReportColumnDefinition>? AvailableColumns,
    List<ReportFilterDefinition>? AvailableFilters,
    List<string>? DefaultColumns,
    string? DefaultSort,
    string? SqlTemplate,
    string? CountSqlTemplate,
    List<FilterSqlMappingItem>? FilterSqlMappings,
    bool? IsEnabled
);
