using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Container;

/// <summary>
/// 合并托盘
/// </summary>
[Table("UnionUnitloads")]
public class UnionUnitload : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? ContainerCode { get; set; }

    #region IAuditable 实现

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    [MaxLength(255)]
    public virtual string? CreatedBy { get; set; }

    #endregion

    /// <summary>
    /// 重量
    /// </summary>
    public virtual decimal? Weight { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    public virtual decimal? Height { get; set; }

    /// <summary>
    /// 长度
    /// </summary>
    public virtual decimal? Length { get; set; }

    /// <summary>
    /// 宽度
    /// </summary>
    public virtual decimal? Width { get; set; }

    /// <summary>
    /// 体积
    /// </summary>
    public virtual decimal? Volume { get; set; }

    /// <summary>
    /// 存储组
    /// </summary>
    [MaxLength(255)]
    public virtual string? StorageGroup { get; set; }

    /// <summary>
    /// 出库标志
    /// </summary>
    [MaxLength(255)]
    public virtual string? OutFlag { get; set; }

    /// <summary>
    /// 容器规格
    /// </summary>
    [MaxLength(255)]
    public virtual string? ContainerSpecification { get; set; }

    /// <summary>
    /// 是否已归档
    /// </summary>
    public virtual bool? Archived { get; set; }

    /// <summary>
    /// 归档时间
    /// </summary>
    public virtual DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 操作提示类型
    /// </summary>
    [MaxLength(255)]
    public virtual string? OpHintType { get; set; }

    /// <summary>
    /// 操作提示信息
    /// </summary>
    [MaxLength(255)]
    public virtual string? OpHintInfo { get; set; }

    /// <summary>
    /// 库位ID
    /// </summary>
    public virtual int? LocationId { get; set; }

    /// <summary>
    /// 当前库位时间
    /// </summary>
    public virtual DateTime? CurrentLocationTime { get; set; }

    /// <summary>
    /// 是否正在移动
    /// </summary>
    public virtual bool? BeingMoved { get; set; }

    /// <summary>
    /// 是否已分配
    /// </summary>
    public virtual bool? Allocated { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
