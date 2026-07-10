using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 用户报表配置
/// </summary>
[Table("UserReportConfigs")]
public class UserReportConfig : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

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
    /// 报表编码
    /// </summary>
    [MaxLength(50)]
    public virtual string? ReportCode { get; set; }

    /// <summary>
    /// 配置名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public virtual string? ConfigName { get; set; }

    /// <summary>
    /// 选中的列 field 列表（JSON）
    /// </summary>
    public virtual string? SelectedColumns { get; set; }

    /// <summary>
    /// 列顺序（JSON）
    /// </summary>
    public virtual string? ColumnOrder { get; set; }

    /// <summary>
    /// 列宽（JSON）
    /// </summary>
    public virtual string? ColumnWidths { get; set; }

    /// <summary>
    /// 固定筛选条件值（JSON）
    /// </summary>
    public virtual string? FixedFilters { get; set; }

    /// <summary>
    /// 排序配置（JSON）
    /// </summary>
    public virtual string? SortConfig { get; set; }

    /// <summary>
    /// 是否为默认配置
    /// </summary>
    public virtual bool IsDefault { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime CreatedTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime ModifiedTime { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
