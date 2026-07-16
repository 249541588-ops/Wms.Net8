using global::System.Collections.Generic;
using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Container;

/// <summary>
/// 托盘/容器
/// </summary>
[Table("Unitloads")]
public class Unitload : IEntity<int>, IAuditable
{
    /// <summary>
    /// 托盘ID
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int UnitloadId { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public virtual int? Version { get; set; }

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(100)]
    public virtual string? ContainerCode { get; set; }

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
    /// 重量
    /// </summary>
    public virtual decimal? Weight { get; set; } = 0.0M;

    /// <summary>
    /// 高度
    /// </summary>
    public virtual decimal? Height { get; set; } = 0.0M;

    /// <summary>
    /// 长度
    /// </summary>
    public virtual decimal? Length { get; set; } = 0.0M;

    /// <summary>
    /// 宽度
    /// </summary>
    public virtual decimal? Width { get; set; } = 0.0M;

    /// <summary>
    /// 体积
    /// </summary>
    public virtual decimal? Volume { get; set; } = 0.0M;

    /// <summary>
    /// 存储组
    /// </summary>
    [MaxLength(255)]
    public virtual string? StorageGroup { get; set; } = Cst.普通;

    /// <summary>
    /// 出库标志
    /// </summary>
    [MaxLength(255)]
    public virtual string? OutFlag { get; set; } = string.Empty;

    /// <summary>
    /// 容器规格
    /// </summary>
    [MaxLength(255)]
    public virtual string? ContainerSpecification { get; set; } = Cst.普通托盘;

    /// <summary>
    /// 是否有计数错误
    /// </summary>
    public virtual bool? HasCountingError { get; set; } = false;

    /// <summary>
    /// 消息错误
    /// </summary>
    [MaxLength(1000)]
    public virtual string? HasMsgError { get; set; } = string.Empty;

    /// <summary>
    /// 是否零散
    /// </summary>
    public virtual bool? Odd { get; set; } = false;

    /// <summary>
    /// 是否正在移动
    /// </summary>
    public virtual bool? BeingMoved { get; set; } = false;

    /// <summary>
    /// 是否已分配
    /// </summary>
    public virtual bool? Allocated { get; set; } = false;

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
    public virtual string? OpHintType { get; set; } = string.Empty;

    /// <summary>
    /// 操作提示信息
    /// </summary>
    [MaxLength(255)]
    public virtual string? OpHintInfo { get; set; } = string.Empty;

    /// <summary>
    /// 出库单键值
    /// </summary>
    public virtual int? outboundorder_key { get; set; } = null;

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
    public virtual bool? IsExcludeCurrentUnitload { get; set; } = false;

    /// <summary>
    /// 是否已上传
    /// </summary>
    public virtual bool? IsUpload { get; set; } = false;

    /// <summary>
    /// 是否预分容
    /// </summary>
    public virtual int? IsAdvance { get; set; } = 0;

    /// <summary>
    /// 是否补电
    /// </summary>
    public virtual int? IsSupplement { get; set; } = 0;

    /// <summary>
    /// 是否发给杭可
    /// </summary>
    public virtual int IsToHangke { get; set; } = 0;

    /// <summary>
    /// 绑定的工艺路线ID（null 表示使用硬编码模式）
    /// </summary>
    public virtual int? ProcessRouteId { get; set; }

    /// <summary>
    /// 锁定的工艺路线版本ID（快照引用，路线修改不影响运行中托盘）
    /// </summary>
    public virtual int? ProcessRouteVersionId { get; set; }

    /// <summary>
    /// 当前在路线中的步骤ID
    /// </summary>
    public virtual int? CurrentStepId { get; set; }

    /// <summary>
    /// 下一个步骤ID（分支等待人工选择时为 null）
    /// </summary>
    public virtual int? NextStepId { get; set; }

    /// <summary>
    /// 是否等待分支选择（true 时前端需弹出选择界面）
    /// </summary>
    public virtual bool? IsAwaitingBranchSelection { get; set; } = false;

    /// <summary>
    /// 库位
    /// </summary>
    public virtual Location? Location { get; set; }

    /// <summary>
    /// 托盘明细集合
    /// </summary>
    [InverseProperty("Unitload")]
    public virtual ICollection<UnitloadItem>? UnitloadItems { get; set; }

    #region IEntity 成员

    int IEntity<int>.Id { get => UnitloadId; set => UnitloadId = value; }

    object IEntity.Id => UnitloadId;

    #endregion
}
