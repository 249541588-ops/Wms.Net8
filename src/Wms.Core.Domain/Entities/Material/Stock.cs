using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Material;

/// <summary>
/// 库存
/// </summary>
[Table("Stocks")]
public class Stock : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public virtual int Version { get; set; }

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
    /// 数量
    /// </summary>
    public virtual decimal? Quantity { get; set; }

    /// <summary>
    /// 分配数量
    /// </summary>
    public virtual decimal? AllocatedQuantity { get; set; }

    /// <summary>
    /// 拣选数量
    /// </summary>
    public virtual decimal? PickedQuantity { get; set; }

    /// <summary>
    /// 锁定数量
    /// </summary>
    public virtual decimal? LockedQuantity { get; set; }

    /// <summary>
    /// 生产日期
    /// </summary>
    public virtual DateTime? ProductionDate { get; set; }

    /// <summary>
    /// 过期日期
    /// </summary>
    public virtual DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// 接收时间
    /// </summary>
    public virtual DateTime? ReceivedTime { get; set; }

    /// <summary>
    /// 计量单位
    /// </summary>
    [MaxLength(255)]
    public virtual string? Uom { get; set; }

    /// <summary>
    /// 出库排序
    /// </summary>
    [MaxLength(255)]
    public virtual string? OutOrdering { get; set; }

    /// <summary>
    /// 是否盘点
    /// </summary>
    public virtual bool? Stocktaking { get; set; }

    /// <summary>
    /// 库龄基准日期
    /// </summary>
    public virtual DateTime? AgeBaseline { get; set; }

    /// <summary>
    /// 托盘ID
    /// </summary>
    public virtual int? UnitloadId { get; set; }

    /// <summary>
    /// 库位ID
    /// </summary>
    public virtual int LocationId { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
