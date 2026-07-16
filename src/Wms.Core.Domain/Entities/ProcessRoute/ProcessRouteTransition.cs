using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.ProcessRoute;

/// <summary>
/// 工艺路线转移/边（步骤间的连接）
/// </summary>
[Table("ProcessRouteTransitions")]
public class ProcessRouteTransition : IEntity<int>, IAuditable
{
    /// <summary>
    /// 转移ID
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 所属版本ID
    /// </summary>
    public int VersionId { get; set; }

    /// <summary>
    /// 源步骤ID
    /// </summary>
    public int FromStepId { get; set; }

    /// <summary>
    /// 目标步骤ID
    /// </summary>
    public int ToStepId { get; set; }

    /// <summary>
    /// 转移类型: Sequential(顺序) / Branch(分支)
    /// </summary>
    [Required, MaxLength(20)]
    public string TransitionType { get; set; } = "Sequential";

    /// <summary>
    /// 分支标签（Branch 类型时由操作员选择）
    /// </summary>
    [MaxLength(100)]
    public string? Label { get; set; }

    /// <summary>
    /// 是否为默认分支
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 所属版本
    /// </summary>
    [ForeignKey("VersionId")]
    public virtual ProcessRouteVersion? Version { get; set; }

    /// <summary>
    /// 源步骤
    /// </summary>
    [ForeignKey("FromStepId")]
    public virtual ProcessRouteStep? FromStep { get; set; }

    /// <summary>
    /// 目标步骤
    /// </summary>
    [ForeignKey("ToStepId")]
    public virtual ProcessRouteStep? ToStep { get; set; }

    #region IAuditable 实现

    public DateTime? CreatedTime { get; set; }
    public DateTime? ModifiedTime { get; set; }
    [MaxLength(64)]
    public string? CreatedBy { get; set; }
    [MaxLength(64)]
    public string? ModifiedBy { get; set; }

    #endregion

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
