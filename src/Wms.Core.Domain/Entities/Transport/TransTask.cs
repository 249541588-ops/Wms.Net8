using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Transport;

/// <summary>
/// 运输任务
/// </summary>
[Table("TransTasks")]
public class TransTask : IEntity<int>, IAuditable
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
    /// 任务代码
    /// </summary>
    [MaxLength(255)]
    public virtual string? TaskCode { get; set; }

    /// <summary>
    /// 任务类型
    /// </summary>
    [MaxLength(255)]
    public virtual string? TaskType { get; set; }

    /// <summary>
    /// 托盘ID
    /// </summary>
    public virtual int? UnitloadId { get; set; }

    /// <summary>
    /// 起始库位ID
    /// </summary>
    public virtual int StartLocationId { get; set; }

    /// <summary>
    /// 目标库位ID
    /// </summary>
    public virtual int EndLocationId { get; set; }

    /// <summary>
    /// 是否为WCS任务
    /// </summary>
    public virtual bool? ForWcs { get; set; }

    /// <summary>
    /// 是否已发送至WCS
    /// </summary>
    public virtual bool? WasSentToWcs { get; set; }

    /// <summary>
    /// 发送至WCS的时间
    /// </summary>
    public virtual DateTime? SentToWcsAt { get; set; }

    /// <summary>
    /// 订单编号
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
    /// 托盘
    /// </summary>
    public virtual Unitload? Unitload { get; set; }

    /// <summary>
    /// 起始库位
    /// </summary>
    public virtual Location? StartLocation { get; set; }

    /// <summary>
    /// 目标库位
    /// </summary>
    public virtual Location? EndLocation { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
