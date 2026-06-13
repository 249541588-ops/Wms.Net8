using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Container;

/// <summary>
/// 电池电芯
/// </summary>
[Table("BatteryCells")]
public class BatteryCell : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 物料ID
    /// </summary>
    public virtual int MaterialId { get; set; }

    /// <summary>
    /// 是否发送打包
    /// </summary>
    public virtual int? IsSendPack { get; set; } = 0;

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(20)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// 条码
    /// </summary>
    [MaxLength(50)]
    public virtual string BarCode { get; set; } = string.Empty;

    /// <summary>
    /// X等级
    /// </summary>
    [MaxLength(20)]
    public virtual string? xLevel { get; set; }

    /// <summary>
    /// OCV3
    /// </summary>
    public virtual decimal? OCV3 { get; set; }

    /// <summary>
    /// IR3
    /// </summary>
    public virtual decimal? IR3 { get; set; }

    /// <summary>
    /// V3柯亚
    /// </summary>
    public virtual decimal? V3KeYa { get; set; }

    /// <summary>
    /// OCV4
    /// </summary>
    public virtual decimal? OCV4 { get; set; }

    /// <summary>
    /// IR4
    /// </summary>
    public virtual decimal? IR4 { get; set; }

    /// <summary>
    /// V4柯亚
    /// </summary>
    public virtual decimal? V4KeYa { get; set; }

    /// <summary>
    /// 容量
    /// </summary>
    public virtual decimal? Capacity { get; set; }

    /// <summary>
    /// K值
    /// </summary>
    public virtual decimal? KVal { get; set; }

    /// <summary>
    /// CCP
    /// </summary>
    public virtual decimal? CCP { get; set; }

    /// <summary>
    /// Dcirnz
    /// </summary>
    public virtual decimal? Dcirnz { get; set; }

    /// <summary>
    /// 序列号
    /// </summary>
    [MaxLength(20)]
    public virtual string? Sequence { get; set; }

    /// <summary>
    /// 位置索引
    /// </summary>
    public virtual int? LocIndex { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [MaxLength(20)]
    public virtual string Status { get; set; } = "(0)";

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(2000)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 操作编号
    /// </summary>
    public virtual int? OperationNumber { get; set; } = 1;

    /// <summary>
    /// 是否预先进
    /// </summary>
    public virtual int? IsAdvance { get; set; } = 0;

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(30)]
    public virtual string? ContainerCode { get; set; }

    /// <summary>
    /// 关联物料
    /// </summary>
    public virtual Materials? Material { get; set; }

    #region IAuditable 实现

    public virtual DateTime? CreatedTime { get; set; }
    public virtual DateTime? ModifiedTime { get; set; }
    [MaxLength(64)]
    public virtual string? CreatedBy { get; set; }
    [MaxLength(64)]
    public virtual string? ModifiedBy { get; set; }

    #endregion

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
