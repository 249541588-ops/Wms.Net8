using global::System.ComponentModel.DataAnnotations;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Container;

/// <summary>
/// 合并托盘明细项
/// </summary>
public class UnionUnitloadItem : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

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
    [MaxLength(255)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// 库存状态
    /// </summary>
    [MaxLength(255)]
    public virtual string? StockStatus { get; set; }

    /// <summary>
    /// 数量
    /// </summary>
    public virtual decimal? Quantity { get; set; }

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

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
