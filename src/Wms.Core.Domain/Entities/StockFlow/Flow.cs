using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.StockFlow;

/// <summary>
/// 库存流水
/// </summary>
[Table("Flows")]
public class Flow : IEntity<int>, IAuditable
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
    /// 计量单位
    /// </summary>
    [MaxLength(255)]
    public virtual string? Uom { get; set; }

    /// <summary>
    /// 业务类型
    /// </summary>
    [MaxLength(255)]
    public virtual string? BizType { get; set; }

    /// <summary>
    /// 方向
    /// </summary>
    [MaxLength(255)]
    public virtual string? Direction { get; set; }

    /// <summary>
    /// 操作类型
    /// </summary>
    [MaxLength(255)]
    public virtual string? OpType { get; set; }

    /// <summary>
    /// 事务编号
    /// </summary>
    [MaxLength(255)]
    public virtual string? TxNo { get; set; }

    /// <summary>
    /// 订单编号
    /// </summary>
    [MaxLength(255)]
    public virtual string? OrderCode { get; set; }

    /// <summary>
    /// 业务订单号
    /// </summary>
    [MaxLength(255)]
    public virtual string? BizOrder { get; set; }

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? ContainerCode { get; set; }

    /// <summary>
    /// 余额
    /// </summary>
    public virtual decimal? Balance { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 扩展字段1
    /// </summary>
    [MaxLength(255)]
    public virtual string? Ext1 { get; set; }

    /// <summary>
    /// 扩展字段2
    /// </summary>
    [MaxLength(255)]
    public virtual string? Ext2 { get; set; }

    /// <summary>
    /// 托盘ID
    /// </summary>
    public virtual int? UnitloadId { get; set; }

    /// <summary>
    /// 库位ID
    /// </summary>
    public virtual int? LocationId { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
