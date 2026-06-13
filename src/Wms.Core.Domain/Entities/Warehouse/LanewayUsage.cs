using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Warehouse;

/// <summary>
/// 通道使用情况 - 表示通道的使用统计信息（复合主键）
/// </summary>
[Table("LanewayUsage")]
public class LanewayUsage : IEntity
{
    /// <summary>
    /// 通道编号（主键之一）
    /// </summary>
    public virtual int LanewayId { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime mtime { get; set; }

    /// <summary>
    /// 总计
    /// </summary>
    public virtual int Total { get; set; }

    /// <summary>
    /// 可用
    /// </summary>
    public virtual int Available { get; set; }

    /// <summary>
    /// 已装载
    /// </summary>
    public virtual int Loaded { get; set; }

    /// <summary>
    /// 入库禁用数量
    /// </summary>
    public virtual int InboundDisabled { get; set; }

    /// <summary>
    /// 存储组（主键之一）
    /// </summary>
    [MaxLength(10)]
    public virtual string? StorageGroup { get; set; }

    /// <summary>
    /// 规格（主键之一）
    /// </summary>
    [MaxLength(16)]
    public virtual string? Specification { get; set; }

    /// <summary>
    /// 重量上限（主键之一）
    /// </summary>
    public virtual decimal WeightLimit { get; set; }

    /// <summary>
    /// 高度上限（主键之一）
    /// </summary>
    public virtual decimal HeightLimit { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 LanewayId
    /// </summary>
    object IEntity.Id => LanewayId;

    #endregion
}
