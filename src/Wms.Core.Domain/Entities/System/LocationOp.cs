using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 库位操作记录
/// </summary>
[Table("LocationOps")]
public class LocationOp : IEntity<int>, IAuditable
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
    [MaxLength(64)]
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(64)]
    public virtual string? ModifiedBy { get; set; }

    #endregion

    /// <summary>
    /// 库位ID
    /// </summary>
    public virtual int LocationId { get; set; }

    /// <summary>
    /// 操作类型
    /// </summary>
    [MaxLength(20)]
    public virtual string? OpType { get; set; }

    /// <summary>
    /// URL
    /// </summary>
    [MaxLength(500)]
    public virtual string? Url { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(2000)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 前一状态
    /// </summary>
    [MaxLength(1000)]
    public virtual string? PreviousState { get; set; }

    /// <summary>
    /// 新状态
    /// </summary>
    [MaxLength(1000)]
    public virtual string? NewState { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
