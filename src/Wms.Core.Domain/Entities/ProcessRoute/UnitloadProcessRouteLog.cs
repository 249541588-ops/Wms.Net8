using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.ProcessRoute;

/// <summary>
/// 托盘工艺轨迹日志
/// </summary>
[Table("UnitloadProcessRouteLogs")]
public class UnitloadProcessRouteLog : IEntity<int>, IAuditable
{
    /// <summary>
    /// 日志ID
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 托盘ID
    /// </summary>
    public int UnitloadId { get; set; }

    /// <summary>
    /// 路线版本ID
    /// </summary>
    public int? VersionId { get; set; }

    /// <summary>
    /// 步骤ID
    /// </summary>
    public int? StepId { get; set; }

    /// <summary>
    /// 工序编码
    /// </summary>
    [MaxLength(50)]
    public string? OperationCode { get; set; }

    /// <summary>
    /// 动作类型: Enter(进入工序) / Advance(推进) / BranchSelect(分支选择)
    /// </summary>
    [Required, MaxLength(20)]
    public string ActionType { get; set; } = "Enter";

    /// <summary>
    /// 从哪个工序转移而来
    /// </summary>
    [MaxLength(50)]
    public string? FromOperation { get; set; }

    /// <summary>
    /// 转移到哪个工序
    /// </summary>
    [MaxLength(50)]
    public string? ToOperation { get; set; }

    /// <summary>
    /// 选择的转移ID（分支选择时记录）
    /// </summary>
    public int? SelectedTransitionId { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    [MaxLength(64)]
    public string? Operator { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public string? Remark { get; set; }

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
