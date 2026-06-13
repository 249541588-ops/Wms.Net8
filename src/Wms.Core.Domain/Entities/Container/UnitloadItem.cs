using global::System.Collections.Generic;
using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Material;

namespace Wms.Core.Domain.Entities.Container;

/// <summary>
/// 托盘明细项
/// </summary>
[Table("UnitloadItems")]
public class UnitloadItem
{
    /// <summary>
    /// 托盘明细项ID
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int UnitloadItemId { get; set; }

    /// <summary>
    /// 托盘ID
    /// </summary>
    public virtual int? UnitloadId { get; set; }

    /// <summary>
    /// 物料ID
    /// </summary>
    public virtual int? MaterialId { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(30)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// 库存状态
    /// </summary>
    [MaxLength(30)]
    public virtual string? StockStatus { get; set; } = Cst.合格;

    /// <summary>
    /// 数量
    /// </summary>
    public virtual decimal? Quantity { get; set; }

    /// <summary>
    /// 虚假数量
    /// </summary>
    public virtual decimal? FalseQuantity { get; set; } = 0M;

    /// <summary>
    /// 计量单位
    /// </summary>
    [MaxLength(255)]
    public virtual string? Uom { get; set; }

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
    [MaxLength(100)]
    public virtual string? BoxCode { get; set; }

    /// <summary>
    /// 位置
    /// </summary>
    public virtual int? Position { get; set; } = 0;

    /// <summary>
    /// X等级
    /// </summary>
    [MaxLength(20)]
    public virtual string? xLevel { get; set; } = string.Empty;

    /// <summary>
    /// 操作编号
    /// </summary>
    public virtual int? OperationNumber { get; set; }

    /// <summary>
    /// 批次号
    /// </summary>
    public virtual int? BatchNumber { get; set; } = 0;

    /// <summary>
    /// 是否预分容
    /// </summary>
    public virtual int? IsAdvance { get; set; } = 0;

    /// <summary>
    /// 是否补充
    /// </summary>
    public virtual int? IsSupplement { get; set; } = 0;

    /// <summary>
    /// 托盘
    /// </summary>
    [ForeignKey("UnitloadId")]
    public virtual Unitload? Unitload { get; set; }

    /// <summary>
    /// 物料ID
    /// </summary>
    public virtual Materials? Material { get; set; }

    /// <summary>
    /// 电芯明细集合
    /// </summary>
    [InverseProperty("UnitloadItem")]
    public virtual ICollection<UnitloadItemDetail>? UnitloadItemDetails { get; set; }

}
