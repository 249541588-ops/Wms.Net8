using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Outbound;

/// <summary>
/// 出库批次
/// </summary>
[Table("OutboundBatch")]
public class OutboundBatch : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 巷道ID
    /// </summary>
    public virtual int LanewayId { get; set; }

    /// <summary>
    /// 物料ID
    /// </summary>
    public virtual int MaterialId { get; set; }

    /// <summary>
    /// 当前操作
    /// </summary>
    [MaxLength(20)]
    public virtual string? CurrentOperation { get; set; }

    /// <summary>
    /// 工艺次数
    /// </summary>
    public virtual int? OperationNumber { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(20)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// X等级
    /// </summary>
    [MaxLength(20)]
    public virtual string? xLevel { get; set; }

    /// <summary>
    /// 需求数量
    /// </summary>
    public virtual int QuantityRequired { get; set; } = 0;

    /// <summary>
    /// 已交付数量
    /// </summary>
    public virtual int QuantityDelivered { get; set; } = 0;

    /// <summary>
    /// 分容类型
    /// </summary>
    public virtual int IsAdvance { get; set; } = 0;

    /// <summary>
    /// 是否补电
    /// </summary>
    public virtual int IsSupplement { get; set; } = 0;

    /// <summary>
    /// 状态
    /// </summary>
    public virtual int Status { get; set; } = 0;

    /// <summary>
    /// 排序
    /// </summary>
    public virtual int? Sort { get; set; }

    /// <summary>
    /// 错误计数
    /// </summary>
    public virtual int ErrorCount { get; set; } = 0;

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(250)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    [MaxLength(64)]
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(64)]
    public virtual string? ModifiedBy { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
