using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.ProcessRoute;

/// <summary>
/// 工艺路线版本
/// </summary>
[Table("ProcessRouteVersions")]
public class ProcessRouteVersion : IEntity<int>, IAuditable
{
    /// <summary>
    /// 版本ID
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 所属路线ID
    /// </summary>
    public int ProcessRouteId { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 版本状态: Draft / Published / Archived
    /// </summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// 版本说明
    /// </summary>
    [MaxLength(500)]
    public string? ChangeLog { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? PublishedTime { get; set; }

    /// <summary>
    /// 发布人
    /// </summary>
    [MaxLength(64)]
    public string? PublishedBy { get; set; }

    /// <summary>
    /// 所属路线
    /// </summary>
    [ForeignKey("ProcessRouteId")]
    public virtual ProcessRoute? Route { get; set; }

    /// <summary>
    /// 步骤列表
    /// </summary>
    public virtual ICollection<ProcessRouteStep>? Steps { get; set; }

    /// <summary>
    /// 转移列表
    /// </summary>
    public virtual ICollection<ProcessRouteTransition>? Transitions { get; set; }

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
