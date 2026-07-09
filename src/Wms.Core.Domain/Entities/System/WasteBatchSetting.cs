using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 废料批次设置
/// </summary>
[Table("WasteBatchSetting")]
public class WasteBatchSetting : IEntity<int>, IAuditable
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 是否内置
    /// </summary>
    public virtual bool? IsBuiltIn { get; set; }

    /// <summary>
    /// 库位类型 1.化成 2.V3
    /// </summary>
    public virtual int? LocationType { get; set; }

    /// <summary>
    /// 库位编码
    /// </summary>
    [Required]
    [MaxLength(10)]
    public virtual string LocationCode { get; set; } = string.Empty;

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(50)]
    public virtual string? ContainerCode { get; set; }

    /// <summary>
    /// 批次号
    /// </summary>
    [Required]
    [MaxLength(15)]
    public virtual string Batch { get; set; } = string.Empty;

    #region IAuditable
    public virtual DateTime? CreatedTime { get; set; }
    public virtual DateTime? ModifiedTime { get; set; }
    public virtual string? CreatedBy { get; set; }
    public virtual string? ModifiedBy { get; set; }
    #endregion

    object IEntity.Id => Id;

    public WasteBatchSetting()
    {
        CreatedTime = DateTime.Now;
        ModifiedTime = DateTime.Now;
    }
}
