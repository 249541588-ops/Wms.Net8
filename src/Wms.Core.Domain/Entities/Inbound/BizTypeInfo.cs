using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Inbound;

/// <summary>
/// 业务类型信息
/// </summary>
[Table("BizTypeInfos")]
public class BizTypeInfo : IAuditable
{
    /// <summary>
    /// 业务类型编码（主键）
    /// </summary>
    public virtual string BizTypeCode { get; set; } = string.Empty;

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
    [MaxLength(20)]
    public virtual string? BizType { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [MaxLength(255)]
    public virtual string? Description { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public virtual bool? Enabled { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 适用于入库订单
    /// </summary>
    public virtual bool? AppliesToInboundOrders { get; set; }

    /// <summary>
    /// 适用于出库订单
    /// </summary>
    public virtual bool? AppliesToOutboundOrders { get; set; }

    /// <summary>
    /// 显示顺序
    /// </summary>
    public virtual int? DisplayOrder { get; set; }

    /// <summary>
    /// 选项
    /// </summary>
    [MaxLength(255)]
    public virtual string? Options { get; set; }
}
