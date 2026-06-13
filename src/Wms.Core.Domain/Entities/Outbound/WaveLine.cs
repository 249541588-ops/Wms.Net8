using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Outbound;

/// <summary>
/// 波次行
/// </summary>
[Table("WaveLines")]
public class WaveLine : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 波次ID
    /// </summary>
    public virtual int WaveId { get; set; }

    /// <summary>
    /// 出库行ID
    /// </summary>
    public virtual int? OutboundLineId { get; set; }

    /// <summary>
    /// 需求数量
    /// </summary>
    public virtual decimal? QuantityRequired { get; set; }

    /// <summary>
    /// 行号
    /// </summary>
    public virtual int? LineNumber { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
