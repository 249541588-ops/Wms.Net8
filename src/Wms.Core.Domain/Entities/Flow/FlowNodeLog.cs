using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;

namespace Wms.Core.Domain.Entities.Flow;

/// <summary>
/// 节点执行日志
/// </summary>
[Table("FlowNodeLogs")]
public class FlowNodeLog
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 流程实例 ID
    /// </summary>
    public int InstanceId { get; set; }

    /// <summary>
    /// 节点执行顺序
    /// </summary>
    public int NodeOrder { get; set; }

    /// <summary>
    /// 节点类型
    /// </summary>
    [MaxLength(100)]
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// 节点显示名称
    /// </summary>
    [MaxLength(100)]
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// 执行状态（Success / Skipped / Failed）
    /// </summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 输入数据（JSON）
    /// </summary>
    [MaxLength(4000)]
    public string? InputJson { get; set; }

    /// <summary>
    /// 输出数据（JSON）
    /// </summary>
    [MaxLength(4000)]
    public string? OutputJson { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMsg { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// 所属实例导航属性
    /// </summary>
    public virtual FlowInstance? Instance { get; set; }
}
