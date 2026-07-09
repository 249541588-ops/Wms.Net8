using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 报表导出任务
/// </summary>
[Table("ReportExportTasks")]
public class ReportExportTask : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// Hangfire 任务ID（唯一）
    /// </summary>
    [Required]
    [MaxLength(100)]
    public virtual string? TaskId { get; set; }

    /// <summary>
    /// 报表编码
    /// </summary>
    [MaxLength(50)]
    public virtual string? ReportCode { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public virtual int UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [MaxLength(100)]
    public virtual string? UserName { get; set; }

    /// <summary>
    /// 状态: Pending/Processing/Completed/Failed
    /// </summary>
    [MaxLength(20)]
    public virtual string? Status { get; set; }

    /// <summary>
    /// 导出时的筛选条件（JSON）
    /// </summary>
    public virtual string? FilterParams { get; set; }

    /// <summary>
    /// 导出时的列配置（JSON）
    /// </summary>
    public virtual string? ColumnConfig { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    [MaxLength(200)]
    public virtual string? FileName { get; set; }

    /// <summary>
    /// 文件路径
    /// </summary>
    [MaxLength(500)]
    public virtual string? FilePath { get; set; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public virtual long FileSize { get; set; }

    /// <summary>
    /// 总行数
    /// </summary>
    public virtual int TotalRows { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public virtual string? ErrorMessage { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public virtual DateTime? StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public virtual DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime CreatedTime { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
