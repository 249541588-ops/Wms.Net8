using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Outbound;

/// <summary>
/// 出库行分配
/// </summary>
[Table("OutboundLineAllocations")]
public class OutboundLineAllocation : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 出库行ID
    /// </summary>
    public virtual int? OutboundLineId { get; set; }

    /// <summary>
    /// 库存ID
    /// </summary>
    public virtual int? StockId { get; set; }

    /// <summary>
    /// 托盘项ID
    /// </summary>
    public virtual int? UnitloadItemId { get; set; }

    /// <summary>
    /// 分配数量
    /// </summary>
    public virtual decimal Quantity { get; set; }

    /// <summary>
    /// 分配时间
    /// </summary>
    public virtual DateTime AllocatedAt { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
