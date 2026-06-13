using global::System.ComponentModel.DataAnnotations;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Container;

/// <summary>
/// 电池操作日志
/// </summary>
public class BatteryOp : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(20)]
    public virtual string? ContainerCode { get; set; }

    /// <summary>
    /// 条码
    /// </summary>
    [MaxLength(30)]
    public virtual string? BarCode { get; set; }

    /// <summary>
    /// 操作类型
    /// </summary>
    [MaxLength(20)]
    public virtual string? OpType { get; set; }

    /// <summary>
    /// X等级
    /// </summary>
    [MaxLength(20)]
    public virtual string? xLevel { get; set; }

    /// <summary>
    /// 位置索引
    /// </summary>
    public virtual int? LocIndex { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [MaxLength(20)]
    public virtual string? Status { get; set; } = "(0)";

    /// <summary>
    /// 备注
    /// </summary>
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime CreateAt { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    [MaxLength(30)]
    public virtual string? CreateUser { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
