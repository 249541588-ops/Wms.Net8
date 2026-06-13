using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Counting;

/// <summary>
/// 盘点行项
/// </summary>
[Table("CountingLineItems")]
public class CountingLineItem : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 盘点行ID
    /// </summary>
    public virtual int CountingLineId { get; set; }

    /// <summary>
    /// 托盘项
    /// </summary>
    public virtual int? UnitloadItem { get; set; }

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
    /// 账面数量
    /// </summary>
    public virtual decimal? BookQuantity { get; set; }

    /// <summary>
    /// 盘点数量
    /// </summary>
    public virtual decimal? CountedQuantity { get; set; }

    /// <summary>
    /// 调整原因
    /// </summary>
    [MaxLength(255)]
    public virtual string? AdjustmentReason { get; set; }

    /// <summary>
    /// 生产时间
    /// </summary>
    public virtual DateTime? ProductionTime { get; set; }

    /// <summary>
    /// 出库排序
    /// </summary>
    [MaxLength(255)]
    public virtual string? OutOrdering { get; set; }

    /// <summary>
    /// 箱码
    /// </summary>
    [MaxLength(255)]
    public virtual string? BoxCode { get; set; }

    /// <summary>
    /// 计量单位
    /// </summary>
    [MaxLength(255)]
    public virtual string? Uom { get; set; }

    /// <summary>
    /// 库存数量
    /// </summary>
    public virtual decimal? InventoryQuantity { get; set; }

    /// <summary>
    /// 实际数量
    /// </summary>
    public virtual decimal? ActualQuantity { get; set; }

    /// <summary>
    /// 差异数量
    /// </summary>
    public virtual decimal? DifferenceQuantity { get; set; }

    /// <summary>
    /// 核实数量
    /// </summary>
    public virtual decimal? VerifyQuantity { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
