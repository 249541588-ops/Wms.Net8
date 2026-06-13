using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Outbound;

/// <summary>
/// 波次
/// </summary>
[Table("Waves")]
public class Wave : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public virtual int Version { get; set; }

    /// <summary>
    /// 波次编号
    /// </summary>
    [MaxLength(255)]
    public virtual string? WaveCode { get; set; }

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
    /// 波次名称
    /// </summary>
    [MaxLength(255)]
    public virtual string? WaveName { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [MaxLength(255)]
    public virtual string? Status { get; set; }

    /// <summary>
    /// 计划发货时间
    /// </summary>
    public virtual DateTime? PlannedShipTime { get; set; }

    /// <summary>
    /// 实际发货时间
    /// </summary>
    public virtual DateTime? ActualShipTime { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
