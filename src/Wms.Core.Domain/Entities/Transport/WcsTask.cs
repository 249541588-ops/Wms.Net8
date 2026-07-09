namespace Wms.Core.Domain.Entities.Transport;

/// <summary>
/// WCS 搬运任务（ctask.dbo.wcs_tasks 映射类）
/// </summary>
/// <remarks>
/// 纯 POCO 类，仅用于 Dapper 映射外部 ctask 数据库的 wcs_tasks 表。
/// snake_case → PascalCase 映射由 Dapper.DefaultTypeMap.MatchNamesWithUnderscores 处理。
/// </remarks>
public class WcsTask
{
    public string TaskCode { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string ContCode { get; set; } = string.Empty;
    public string ContType { get; set; } = string.Empty;
    public string StartLoc { get; set; } = string.Empty;
    public string EndLoc { get; set; } = string.Empty;
    public string? ActEndLoc { get; set; }
    public int Prio { get; set; }
    public DateTime SentAt { get; set; }
    public string WmsState { get; set; } = string.Empty;

    /// <summary>
    /// WMS 失败次数（原表拼写 wms_failuir_times）
    /// </summary>
    public int WmsFailuirTimes { get; set; }

    public string WcsState { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public string? LocationGroup { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ErrCode { get; set; }
    public string? ErrMsg { get; set; }
    public string? WmsNote { get; set; }
    public string? WcsNote { get; set; }
    public string? Ex1 { get; set; }
    public string? Ex2 { get; set; }
    public string? Warehouse { get; set; }
}
