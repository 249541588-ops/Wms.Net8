using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// OCV3扫描码批次处理
/// </summary>
[Table("Ocv3ScanCodeBatchProcess")]
public class Ocv3ScanCodeBatchProcess : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 库位编码
    /// </summary>
    [MaxLength(10)]
    public virtual string? LocationCode { get; set; }

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(50)]
    public virtual string? ContainerCode { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(15)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime CreateTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime? UpdateTime { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
