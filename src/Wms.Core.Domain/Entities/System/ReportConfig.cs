using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 报表配置
/// </summary>
[Table("ReportConfigs")]
public class ReportConfig : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    #region IAuditable 实现

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    [MaxLength(255)]
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(255)]
    public virtual string? ModifiedBy { get; set; }

    #endregion

    /// <summary>
    /// 报表编码（唯一）
    /// </summary>
    [Required]
    [MaxLength(50)]
    public virtual string? ReportCode { get; set; }

    /// <summary>
    /// 报表名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public virtual string? ReportName { get; set; }

    /// <summary>
    /// 分类: Inventory/InOut/Location/Custom
    /// </summary>
    [MaxLength(50)]
    public virtual string? Category { get; set; }

    /// <summary>
    /// 报表描述
    /// </summary>
    [MaxLength(500)]
    public virtual string? Description { get; set; }

    /// <summary>
    /// 报表类型: System(预置) 或 Custom(自定义)
    /// </summary>
    [MaxLength(20)]
    public virtual string? ReportType { get; set; }

    /// <summary>
    /// 默认显示列 field 列表（JSON）
    /// </summary>
    public virtual string? DefaultColumns { get; set; }

    /// <summary>
    /// 所有可选列定义（JSON）
    /// </summary>
    public virtual string? AvailableColumns { get; set; }

    /// <summary>
    /// 可用筛选条件定义（JSON）
    /// </summary>
    public virtual string? AvailableFilters { get; set; }

    /// <summary>
    /// 默认排序
    /// </summary>
    [MaxLength(200)]
    public virtual string? DefaultSort { get; set; }

    /// <summary>
    /// 自定义 SQL 查询模板（仅 Custom 类型使用）
    /// </summary>
    public virtual string? SqlTemplate { get; set; }

    /// <summary>
    /// COUNT SQL 模板（仅 Custom 类型使用）
    /// </summary>
    public virtual string? CountSqlTemplate { get; set; }

    /// <summary>
    /// 筛选条件到 SQL WHERE 片段的映射（JSON，仅 Custom 类型使用）
    /// </summary>
    public virtual string? FilterSqlMapping { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public virtual bool IsEnabled { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
