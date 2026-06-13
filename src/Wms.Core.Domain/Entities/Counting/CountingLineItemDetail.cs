using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Counting;

/// <summary>
/// 盘点行项明细
/// </summary>
[Table("CountingLineItemDetails")]
public class CountingLineItemDetail : IEntity<int>
{
    /// <summary>
    /// 主键（不自增，由外部赋值）
    /// </summary>
    public virtual int CountingLineItemDetailId { get; set; }

    /// <summary>
    /// 盘点行项ID
    /// </summary>
    public virtual int? CountingLineItemId { get; set; }

    /// <summary>
    /// 条码
    /// </summary>
    [MaxLength(50)]
    public virtual string? BarCode { get; set; }

    /// <summary>
    /// X等级
    /// </summary>
    [MaxLength(20)]
    public virtual string? xLevel { get; set; }

    /// <summary>
    /// 电压
    /// </summary>
    public virtual decimal? Voltage { get; set; }

    /// <summary>
    /// 电阻
    /// </summary>
    public virtual decimal? ElectricResistance { get; set; }

    /// <summary>
    /// 电流
    /// </summary>
    public virtual decimal? ElectricCurrent { get; set; }

    /// <summary>
    /// 序列号
    /// </summary>
    [MaxLength(20)]
    public virtual string? Sequence { get; set; }

    /// <summary>
    /// 位置索引
    /// </summary>
    public virtual int? LocIndex { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [MaxLength(20)]
    public virtual string Status { get; set; } = "(0)";

    #region IEntity 成员

    /// <summary>
    /// IEntity接口实现 - 返回CountingLineItemDetailId
    /// </summary>
    object IEntity.Id => CountingLineItemDetailId;

    #endregion

    /// <summary>
    /// IEntity&lt;int&gt;显式接口实现
    /// </summary>
    int IEntity<int>.Id
    {
        get => CountingLineItemDetailId;
        set => CountingLineItemDetailId = value;
    }
}
