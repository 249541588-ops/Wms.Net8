using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Container;

/// <summary>
/// 托盘操作日志
/// </summary>
[Table("UnitloadOps")]
public class UnitloadOp : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 操作类型
    /// </summary>
    [MaxLength(20)]
    public virtual string? OpType { get; set; }

    /// <summary>
    /// 方向
    /// </summary>
    [MaxLength(10)]
    public virtual string? Direction { get; set; }

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(100)]
    public virtual string ContainerCode { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    public virtual string? Comment { get; set; }

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

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
