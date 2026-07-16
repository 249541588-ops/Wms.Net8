using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Archive;

/// <summary>
/// 归档单元载荷
/// </summary>
[Table("ArchivedUnitloads")]
public class ArchivedUnitload : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(100)]
    public virtual string? ContainerCode { get; set; }

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
    /// 是否有计数错误
    /// </summary>
    public virtual bool? HasCountingError { get; set; }

    /// <summary>
    /// 消息错误
    /// </summary>
    [MaxLength(1000)]
    public virtual string? HasMsgError { get; set; }

    /// <summary>
    /// 库位ID
    /// </summary>
    public virtual int? LocationId { get; set; }

    /// <summary>
    /// 当前库位时间
    /// </summary>
    public virtual DateTime? CurrentLocationTime { get; set; }

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
    /// 归档时间
    /// </summary>
    public virtual DateTime ArchivedAt { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 归档原因
    /// </summary>
    [MaxLength(50)]
    public virtual string? ArchiveReason { get; set; }

    /// <summary>
    /// 操作编号
    /// </summary>
    public virtual int? OperationNumber { get; set; }

    /// <summary>
    /// 当前操作
    /// </summary>
    [MaxLength(50)]
    public virtual string? CurrentOperation { get; set; }

    /// <summary>
    /// 下一操作
    /// </summary>
    [MaxLength(50)]
    public virtual string? NextOperation { get; set; }

    /// <summary>
    /// 是否排除当前托盘
    /// </summary>
    public virtual bool? IsExcludeCurrentUnitload { get; set; }

    /// <summary>
    /// 是否已上传
    /// </summary>
    public virtual bool? IsUpload { get; set; }

    /// <summary>
    /// 是否预分容
    /// </summary>
    public virtual int? IsAdvance { get; set; }

    /// <summary>
    /// 是否补电
    /// </summary>
    public virtual int? IsSupplement { get; set; }

    /// <summary>
    /// 是否发给杭可
    /// </summary>
    public virtual int IsToHangke { get; set; }

    /// <summary>
    /// 绑定的工艺路线ID
    /// </summary>
    public virtual int? ProcessRouteId { get; set; }

    /// <summary>
    /// 锁定的工艺路线版本ID
    /// </summary>
    public virtual int? ProcessRouteVersionId { get; set; }

    /// <summary>
    /// 当前步骤ID
    /// </summary>
    public virtual int? CurrentStepId { get; set; }

    /// <summary>
    /// 下一步骤ID
    /// </summary>
    public virtual int? NextStepId { get; set; }

    /// <summary>
    /// 是否等待分支选择
    /// </summary>
    public virtual bool? IsAwaitingBranchSelection { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
