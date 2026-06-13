using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Inbound;

/// <summary>
/// 入库订单明细行
/// </summary>
[Table("InboundLines")]
public class InboundLine : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 入库订单ID
    /// </summary>
    public virtual int InboundOrderId { get; set; }

    /// <summary>
    /// 物料ID
    /// </summary>
    public virtual int MaterialId { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(255)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// 库存状态
    /// </summary>
    [MaxLength(255)]
    public virtual string? StockStatus { get; set; }

    /// <summary>
    /// 计量单位
    /// </summary>
    [MaxLength(255)]
    public virtual string? Uom { get; set; }

    /// <summary>
    /// 预期数量
    /// </summary>
    public virtual decimal? QuantityExpected { get; set; }

    /// <summary>
    /// 实收数量
    /// </summary>
    public virtual decimal? QuantityReceived { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 行号
    /// </summary>
    public virtual int? LineNumber { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
