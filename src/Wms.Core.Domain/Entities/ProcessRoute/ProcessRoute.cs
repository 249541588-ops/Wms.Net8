using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.ProcessRoute;

/// <summary>
/// 工艺路线主表
/// </summary>
[Table("ProcessRoutes")]
public class ProcessRoute : IEntity<int>, IAuditable
{
    /// <summary>
    /// 路线ID
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ProcessRouteId { get; set; }

    /// <summary>
    /// 路线编码（唯一）
    /// </summary>
    [Required, MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 路线名称
    /// </summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 当前版本号（从 1 开始，每次发布+1）
    /// </summary>
    public int CurrentVersion { get; set; } = 1;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 是否为系统预设（不可删除）
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 匹配优先级（数值越大优先级越高）
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 版本列表
    /// </summary>
    public virtual ICollection<ProcessRouteVersion>? Versions { get; set; }

    /// <summary>
    /// 物料绑定列表
    /// </summary>
    public virtual ICollection<ProcessRouteMaterialBinding>? MaterialBindings { get; set; }

    #region IAuditable 实现

    public DateTime? CreatedTime { get; set; }
    public DateTime? ModifiedTime { get; set; }
    [MaxLength(64)]
    public string? CreatedBy { get; set; }
    [MaxLength(64)]
    public string? ModifiedBy { get; set; }

    #endregion

    #region IEntity 成员

    int IEntity<int>.Id { get => ProcessRouteId; set => ProcessRouteId = value; }
    object IEntity.Id => ProcessRouteId;

    #endregion
}
