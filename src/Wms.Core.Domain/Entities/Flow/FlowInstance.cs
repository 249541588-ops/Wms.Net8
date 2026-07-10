using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Flow;

/// <summary>
/// 流程实例（运行时）
/// </summary>
[Table("FlowInstances")]
public class FlowInstance : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 唯一实例标识
    /// </summary>
    [Required, MaxLength(50)]
    public string InstanceCode { get; set; } = string.Empty;

    /// <summary>
    /// 模板 ID
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// 业务类型（WcsRequest / TaskCompletion）
    /// </summary>
    [Required, MaxLength(50)]
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>
    /// 业务 ID（如 TaskCode / UnitloadId）
    /// </summary>
    [MaxLength(100)]
    public string? BusinessId { get; set; }

    /// <summary>
    /// 实例状态（Running / Completed / Failed）
    /// </summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Running";

    /// <summary>
    /// 当前执行到的节点顺序
    /// </summary>
    public int CurrentNodeOrder { get; set; }

    /// <summary>
    /// 流程上下文（JSON，各节点共享数据）
    /// </summary>
    [MaxLength(4000)]
    public string? ContextJson { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMsg { get; set; }

    /// <summary>
    /// 节点执行日志
    /// </summary>
    public virtual ICollection<FlowNodeLog>? NodeLogs { get; set; }

    #region IAuditable 实现

    public DateTime? CreatedTime { get; set; }
    public DateTime? ModifiedTime { get; set; }
    [MaxLength(64)]
    public string? CreatedBy { get; set; }
    [MaxLength(64)]
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedTime { get; set; }

    #endregion

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion

    /// <summary>
    /// 获取强类型状态（从字符串 Status 解析）
    /// </summary>
    public FlowInstanceStatus GetStatus() => FlowStateMachine.Parse(Status);

    /// <summary>
    /// 安全转换状态（验证合法性后更新）
    /// </summary>
    public void TransitionTo(FlowInstanceStatus newStatus)
    {
        var current = GetStatus();
        if (!FlowStateMachine.CanTransition(current, newStatus))
            throw new InvalidOperationException($"非法状态转换：{current} → {newStatus}");
        Status = FlowStateMachine.ToString(newStatus);
    }
}
