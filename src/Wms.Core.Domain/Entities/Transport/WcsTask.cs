namespace Wms.Core.Domain.Entities.Transport;

/// <summary>
/// WCS 搬运任务（ctask.dbo.wcs_tasks 映射类）
/// </summary>
/// <remarks>
/// 纯 POCO 类，仅用于 Dapper 映射外部 ctask 数据库的 wcs_tasks 表。
/// 不加 [Table] 等 EF Core 特性，该表由 WCS 管理。
/// </remarks>
public class WcsTask
{
    /// <summary>
    /// 任务编码（主键）
    /// </summary>
    public string TaskCode { get; set; } = string.Empty;

    /// <summary>
    /// 任务类型（入库/出库/移库/叠盘/拆盘）
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// 容器编码
    /// </summary>
    public string ContCode { get; set; } = string.Empty;

    /// <summary>
    /// 容器类型
    /// </summary>
    public string ContType { get; set; } = string.Empty;

    /// <summary>
    /// 起始库位编码
    /// </summary>
    public string StartLoc { get; set; } = string.Empty;

    /// <summary>
    /// 目标库位编码
    /// </summary>
    public string EndLoc { get; set; } = string.Empty;

    /// <summary>
    /// 实际到达库位编码
    /// </summary>
    public string? ActEndLoc { get; set; }

    /// <summary>
    /// 优先级
    /// </summary>
    public int Prio { get; set; }

    /// <summary>
    /// 任务下发时间
    /// </summary>
    public DateTime SentAt { get; set; }

    /// <summary>
    /// WMS 端任务状态
    /// </summary>
    public string WmsState { get; set; } = string.Empty;

    /// <summary>
    /// WMS 失败次数（原表拼写 wms_failuir_times）
    /// </summary>
    public int WmsFailuirTimes { get; set; }

    /// <summary>
    /// WCS 端任务状态
    /// </summary>
    public string WcsState { get; set; } = string.Empty;

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 库位组
    /// </summary>
    public string? LocationGroup { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 错误码
    /// </summary>
    public int ErrCode { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrMsg { get; set; }

    /// <summary>
    /// WMS 备注
    /// </summary>
    public string? WmsNote { get; set; }

    /// <summary>
    /// WCS 备注
    /// </summary>
    public string? WcsNote { get; set; }

    /// <summary>
    /// 扩展字段 1
    /// </summary>
    public string? Ex1 { get; set; }

    /// <summary>
    /// 扩展字段 2
    /// </summary>
    public string? Ex2 { get; set; }

    /// <summary>
    /// 仓库
    /// </summary>
    public string? Warehouse { get; set; }
}
