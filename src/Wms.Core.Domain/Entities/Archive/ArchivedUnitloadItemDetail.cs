using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Archive;

/// <summary>
/// 归档单元载荷物料条码明细
/// </summary>
[Table("ArchivedUnitloadItemDetails")]
public class ArchivedUnitloadItemDetail : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 单元载荷物料明细ID
    /// </summary>
    public virtual int? UnitloadItemId { get; set; }

    /// <summary>
    /// 条码
    /// </summary>
    [MaxLength(50)]
    public virtual string? BarCode { get; set; }

    /// <summary>
    /// 层级
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
    /// V3可压
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
    /// V4可压
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
    /// 序号
    /// </summary>
    [MaxLength(20)]
    public virtual string? Sequence { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(50)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 库位索引
    /// </summary>
    public virtual int? LocIndex { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [MaxLength(20)]
    public virtual string? Status { get; set; } = "(0)";

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
