using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Counting;

/// <summary>
/// 盘点订单
/// </summary>
[Table("CountingOrders")]
public class CountingOrder : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

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
    /// 盘点订单编号
    /// </summary>
    [MaxLength(255)]
    public virtual string? CountingOrderCode { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public virtual int? Status { get; set; }

    /// <summary>
    /// 是否关闭
    /// </summary>
    public virtual bool? Closed { get; set; }

    /// <summary>
    /// 关闭时间
    /// </summary>
    public virtual DateTime? ClosedAt { get; set; }

    /// <summary>
    /// 关闭人
    /// </summary>
    [MaxLength(255)]
    public virtual string? ClosedBy { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 物料
    /// </summary>
    public virtual int? Material { get; set; }

    /// <summary>
    /// 库存状态
    /// </summary>
    [MaxLength(255)]
    public virtual string? StockStatus { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(255)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// 盘点模型
    /// </summary>
    [MaxLength(255)]
    public virtual string? CountingModel { get; set; }

    /// <summary>
    /// 货架ID列表
    /// </summary>
    [MaxLength(255)]
    public virtual string? RackIds { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
