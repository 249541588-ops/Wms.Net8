using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.StockFlow;

/// <summary>
/// 批次统计
/// </summary>
[Table("BatchCount")]
public class BatchCount : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(20)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// 批次编号
    /// </summary>
    public virtual int? BatchNumber { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(50)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? ctime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime? mtime { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
