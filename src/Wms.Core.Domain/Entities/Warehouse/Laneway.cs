using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Warehouse;

/// <summary>
/// 通道 - 表示仓库中的通道信息
/// </summary>
[Table("Laneways")]
public class Laneway : IEntity<int>, IAuditable
{
    /// <summary>
    /// 通道编号（主键）
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public virtual int LanewayId { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public virtual int Version { get; set; }

    /// <summary>
    /// 仓库编号
    /// </summary>
    public virtual int? WarehouseId { get; set; }

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
    /// 通道编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? LanewayCode { get; set; }

    /// <summary>
    /// 区域
    /// </summary>
    [MaxLength(255)]
    public virtual string? Area { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 是否自动化
    /// </summary>
    public virtual bool? Automated { get; set; }

    /// <summary>
    /// 是否离线
    /// </summary>
    public virtual bool? Offline { get; set; }

    /// <summary>
    /// 离线备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? OfflineComment { get; set; }

    /// <summary>
    /// 离线时间
    /// </summary>
    public virtual DateTime? TakeOfflineTime { get; set; }

    /// <summary>
    /// 总离线时长（小时）
    /// </summary>
    public virtual double? TotalOfflineHours { get; set; }

    /// <summary>
    /// 是否双深
    /// </summary>
    public virtual bool? DoubleDeep { get; set; }

    /// <summary>
    /// 预留库位数量
    /// </summary>
    public virtual int? ReservedLocationCount { get; set; }
        
    /// <summary>
    /// 仓库编号
    /// </summary>
    public virtual Warehouse? Warehouse { get; set; }

    /// <summary>
    /// 初始化通道类的新实例
    /// </summary>
    public Laneway()
    {
        CreatedTime = DateTime.Now;
        ModifiedTime = DateTime.Now;
    }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 LanewayId
    /// </summary>
    int IEntity<int>.Id { get => LanewayId; set => LanewayId = value; }

    /// <summary>
    /// 显式接口实现 - 返回 LanewayId
    /// </summary>
    object IEntity.Id => LanewayId;

    #endregion
}
