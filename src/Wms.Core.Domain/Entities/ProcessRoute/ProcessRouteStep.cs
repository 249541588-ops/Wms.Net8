using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.ProcessRoute;

/// <summary>
/// 工艺步骤节点
/// </summary>
[Table("ProcessRouteSteps")]
public class ProcessRouteStep : IEntity<int>, IAuditable
{
    /// <summary>
    /// 步骤ID
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 所属版本ID
    /// </summary>
    public int VersionId { get; set; }

    /// <summary>
    /// 工序编码（对应 Unitload_Enum.CurrentOperation 枚举名）
    /// </summary>
    [Required, MaxLength(50)]
    public string OperationCode { get; set; } = string.Empty;

    /// <summary>
    /// 步骤显示名称
    /// </summary>
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 步骤类型: Start / Normal / End
    /// </summary>
    [Required, MaxLength(20)]
    public string StepType { get; set; } = "Normal";

    /// <summary>
    /// 是否为起始步骤
    /// </summary>
    public bool IsStart { get; set; }

    /// <summary>
    /// 是否为终止步骤
    /// </summary>
    public bool IsEnd { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 所属版本
    /// </summary>
    [ForeignKey("VersionId")]
    public virtual ProcessRouteVersion? Version { get; set; }

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
