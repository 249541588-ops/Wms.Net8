using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Container;

/// <summary>
/// 电芯分选
/// </summary>
[Table("BatteryCellSorting")]
public class BatteryCellSorting : IEntity<int>, IAuditable
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
    /// 挑选名称
    /// </summary>
    [MaxLength(100)]
    public virtual string PickName { get; set; } = string.Empty;

    /// <summary>
    /// 挑选ID
    /// </summary>
    [MaxLength(100)]
    public virtual string PickId { get; set; } = string.Empty;

    /// <summary>
    /// 规格
    /// </summary>
    [MaxLength(100)]
    public virtual string XSpecification { get; set; } = string.Empty;

    /// <summary>
    /// 容量最小值
    /// </summary>
    public virtual decimal CapacityMin { get; set; }

    /// <summary>
    /// 容量最大值
    /// </summary>
    public virtual decimal CapacityMax { get; set; }

    /// <summary>
    /// OCV4最小值
    /// </summary>
    public virtual decimal OCV4Min { get; set; }

    /// <summary>
    /// OCV4最大值
    /// </summary>
    public virtual decimal OCV4Max { get; set; }

    /// <summary>
    /// IR4最小值
    /// </summary>
    public virtual decimal IR4Min { get; set; }

    /// <summary>
    /// IR4最大值
    /// </summary>
    public virtual decimal IR4Max { get; set; }

    /// <summary>
    /// K值最小值
    /// </summary>
    public virtual decimal KValMin { get; set; }

    /// <summary>
    /// K值最大值
    /// </summary>
    public virtual decimal KValMax { get; set; }

    /// <summary>
    /// Dcirnz最小值
    /// </summary>
    public virtual decimal DcirnzMin { get; set; }

    /// <summary>
    /// Dcirnz最大值
    /// </summary>
    public virtual decimal DcirnzMax { get; set; }

    /// <summary>
    /// 通道
    /// </summary>
    [MaxLength(100)]
    public virtual string Passageway { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public virtual short IsEnable { get; set; } = 1;

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

    object IEntity.Id => Id;

    #endregion
}
