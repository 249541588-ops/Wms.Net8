using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Archive;

/// <summary>
/// 归档任务
/// </summary>
[Table("ArchivedTasks")]
public class ArchivedTask : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 任务编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? TaskCode { get; set; }

    /// <summary>
    /// 任务类型
    /// </summary>
    [MaxLength(255)]
    public virtual string? TaskType { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 单元载荷编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? UnitloadCode { get; set; }

    /// <summary>
    /// 起始库位编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? FromLocationCode { get; set; }

    /// <summary>
    /// 目标库位编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? ToLocationCode { get; set; }

    /// <summary>
    /// 实际库位编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? ActualLocationCode { get; set; }

    /// <summary>
    /// 是否WCS任务
    /// </summary>
    public virtual bool? ForWcs { get; set; }

    /// <summary>
    /// 是否已发送至WCS
    /// </summary>
    public virtual bool? WasSentToWcs { get; set; }

    /// <summary>
    /// 发送至WCS时间
    /// </summary>
    public virtual DateTime? SentToWcsAt { get; set; }

    /// <summary>
    /// 订单编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? OrderCode { get; set; }

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
    /// 仓库
    /// </summary>
    [MaxLength(255)]
    public virtual string? WareHouse { get; set; }

    /// <summary>
    /// 库位组
    /// </summary>
    [MaxLength(255)]
    public virtual string? LocationGroup { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [MaxLength(255)]
    public virtual string? Status { get; set; }

    /// <summary>
    /// 归档时间
    /// </summary>
    public virtual DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// 是否取消
    /// </summary>
    public virtual bool? Cancelled { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
