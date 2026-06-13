using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Flow;

/// <summary>
/// 流程模板
/// </summary>
[Table("FlowTemplates")]
public class FlowTemplate : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 模板名称（如 "标准入库"）
    /// </summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模板编码（如 "INBOUND_STANDARD"）
    /// </summary>
    [Required, MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 流程分类（入库/出库/移库/质检/盘点）
    /// </summary>
    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 流程阶段（Request = 请求阶段, Completion = 完成阶段）
    /// </summary>
    [Required, MaxLength(20)]
    public string Phase { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 是否为系统预设模板（不可删除）
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 匹配优先级（数值越大优先级越高）
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 条件匹配规则（JSON）
    /// </summary>
    [MaxLength(2000)]
    public string? MatchRules { get; set; }

    /// <summary>
    /// 流程节点列表
    /// </summary>
    public virtual ICollection<FlowNode>? Nodes { get; set; }

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
