using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Outbound;

/// <summary>
/// 出库订单
/// </summary>
[Table("OutboundOrders")]
public class OutboundOrder : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public virtual int Version { get; set; }

    /// <summary>
    /// 出库订单编号
    /// </summary>
    [MaxLength(255)]
    public virtual string? OutboundOrderCode { get; set; }

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
    /// 业务类型
    /// </summary>
    [MaxLength(255)]
    public virtual string? BizType { get; set; }

    /// <summary>
    /// 业务订单号
    /// </summary>
    [MaxLength(255)]
    public virtual string? BizOrder { get; set; }

    /// <summary>
    /// 发货至
    /// </summary>
    [MaxLength(255)]
    public virtual string? ShipTo { get; set; }

    /// <summary>
    /// 发货方式
    /// </summary>
    [MaxLength(255)]
    public virtual string? ShipBy { get; set; }

    /// <summary>
    /// 最晚发货时间
    /// </summary>
    public virtual DateTime? ShipBefore { get; set; }

    /// <summary>
    /// 订单发起人
    /// </summary>
    [MaxLength(255)]
    public virtual string? OrderBy { get; set; }

    /// <summary>
    /// 审批人
    /// </summary>
    [MaxLength(255)]
    public virtual string? ApprovedBy { get; set; }

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
    /// 状态
    /// </summary>
    [MaxLength(255)]
    public virtual string? Status { get; set; }

    /// <summary>
    /// 波次
    /// </summary>
    public virtual int? Wave { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
