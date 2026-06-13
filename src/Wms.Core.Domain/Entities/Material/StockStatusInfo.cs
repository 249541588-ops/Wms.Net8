using global::System.ComponentModel.DataAnnotations;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Material;

/// <summary>
/// 库存状态信息
/// </summary>
public class StockStatusInfo : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

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
    /// 库存状态
    /// </summary>
    [MaxLength(20)]
    public virtual string? StockStatus { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [MaxLength(255)]
    public virtual string? Description { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public virtual bool? Enabled { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 显示顺序
    /// </summary>
    public virtual int? DisplayOrder { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
