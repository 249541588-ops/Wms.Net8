using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;

namespace Wms.Core.Domain.Entities.Flow;

/// <summary>
/// 流程节点定义
/// </summary>
[Table("FlowNodes")]
public class FlowNode
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 所属模板 ID
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// 节点类型（对应 INodeHandler.NodeType，如 "ValidateParams" / "FindUnitload"）
    /// </summary>
    [Required, MaxLength(100)]
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// 节点显示名称
    /// </summary>
    [Required, MaxLength(100)]
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// 执行顺序
    /// </summary>
    public int StepOrder { get; set; }

    /// <summary>
    /// 节点专属配置（JSON）
    /// </summary>
    [MaxLength(2000)]
    public string? ConfigJson { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 失败策略（Stop = 停止, Skip = 跳过）
    /// </summary>
    [MaxLength(20)]
    public string? OnFailure { get; set; }

    /// <summary>
    /// 跳过条件表达式（可选）
    /// </summary>
    [MaxLength(500)]
    public string? SkipCondition { get; set; }

    /// <summary>
    /// 事务断点：此节点执行成功后执行 SaveChanges + Commit
    /// </summary>
    public bool IsTransactionBoundary { get; set; }

    /// <summary>
    /// 事务后节点：标记为 true 的节点在事务提交后执行（适用于 MES 上传、杭可通知等外部 HTTP 调用）
    /// </summary>
    public bool IsPostTransaction { get; set; }

    /// <summary>
    /// 软删除标记：用户在前端删除节点时标记为 true，种子同步不会恢复被软删除的节点
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 所属模板导航属性
    /// </summary>
    public virtual FlowTemplate? Template { get; set; }
}
