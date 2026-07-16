using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.ProcessRoute;

/// <summary>
/// 物料-路线绑定（多对多关联表）
/// </summary>
[Table("ProcessRouteMaterialBindings")]
public class ProcessRouteMaterialBinding : IEntity<int>, IAuditable
{
    /// <summary>
    /// 绑定ID
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 路线ID
    /// </summary>
    public int ProcessRouteId { get; set; }

    /// <summary>
    /// 物料ID
    /// </summary>
    public int MaterialId { get; set; }

    /// <summary>
    /// 匹配优先级（数值越大优先级越高）
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 路线
    /// </summary>
    [ForeignKey("ProcessRouteId")]
    public virtual ProcessRoute? Route { get; set; }

    /// <summary>
    /// 物料
    /// </summary>
    [ForeignKey("MaterialId")]
    public virtual Materials? Material { get; set; }

    #region IAuditable 实现

    public DateTime? CreatedTime { get; set; }
    public DateTime? ModifiedTime { get; set; }
    [MaxLength(64)]
    public string? CreatedBy { get; set; }
    [MaxLength(64)]
    public string? ModifiedBy { get; set; }

    #endregion

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
