using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Archive;

/// <summary>
/// 归档单元载荷物料明细
/// </summary>
[Table("ArchivedUnitloadItems")]
public class ArchivedUnitloadItem : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 单元载荷ID
    /// </summary>
    public virtual int UnitloadId { get; set; }

    /// <summary>
    /// 物料ID
    /// </summary>
    public virtual int MaterialId { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(20)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// 库存状态
    /// </summary>
    [MaxLength(10)]
    public virtual string? StockStatus { get; set; }

    /// <summary>
    /// 数量
    /// </summary>
    public virtual decimal Quantity { get; set; }

    /// <summary>
    /// 虚假数量
    /// </summary>
    public virtual decimal? FalseQuantity { get; set; }

    /// <summary>
    /// 计量单位
    /// </summary>
    [MaxLength(8)]
    public virtual string? Uom { get; set; }

    /// <summary>
    /// 生产时间
    /// </summary>
    public virtual DateTime ProductionTime { get; set; }

    /// <summary>
    /// 出库排序
    /// </summary>
    [MaxLength(255)]
    public virtual string? OutOrdering { get; set; }

    /// <summary>
    /// 箱号
    /// </summary>
    [MaxLength(100)]
    public virtual string? BoxCode { get; set; }

    /// <summary>
    /// 位置
    /// </summary>
    public virtual int? Position { get; set; }

    /// <summary>
    /// 层级
    /// </summary>
    [MaxLength(20)]
    public virtual string? xLevel { get; set; }

    /// <summary>
    /// 操作序号
    /// </summary>
    public virtual int OperationNumber { get; set; } = 1;

    /// <summary>
    /// 批次编号
    /// </summary>
    public virtual int? BatchNumber { get; set; }

    /// <summary>
    /// 是否预投
    /// </summary>
    public virtual int? IsAdvance { get; set; }

    /// <summary>
    /// 是否补投
    /// </summary>
    public virtual int? IsSupplement { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
