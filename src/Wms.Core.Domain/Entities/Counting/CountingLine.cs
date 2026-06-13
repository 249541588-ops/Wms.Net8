using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Counting;

/// <summary>
/// 盘点行
/// </summary>
[Table("CountingLines")]
public class CountingLine : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 盘点订单ID
    /// </summary>
    public virtual int CountingOrderId { get; set; }

    /// <summary>
    /// 库位ID
    /// </summary>
    public virtual int LocationId { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [MaxLength(255)]
    public virtual string? Status { get; set; }

    /// <summary>
    /// 托盘
    /// </summary>
    public virtual int? Unitload { get; set; }

    /// <summary>
    /// 物料
    /// </summary>
    public virtual int? Material { get; set; }

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
    /// 系统数量
    /// </summary>
    public virtual decimal? SystemQuantity { get; set; }

    /// <summary>
    /// 实盘数量
    /// </summary>
    public virtual decimal? ActualQuantity { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 行号
    /// </summary>
    public virtual int LineNumber { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
