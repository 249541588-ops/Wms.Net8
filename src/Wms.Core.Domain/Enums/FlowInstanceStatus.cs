namespace Wms.Core.Domain.Enums;

/// <summary>
/// 流程实例状态
/// </summary>
/// <remarks>
/// 状态流转：Running → Completed / Failed
/// </remarks>
public enum FlowInstanceStatus
{
    /// <summary>
    /// 运行中
    /// </summary>
    Running = 0,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 1,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 2
}

/// <summary>
/// FlowInstance 状态机（不可变值对象，提供合法转换验证）
/// </summary>
public static class FlowStateMachine
{
    private static readonly HashSet<(FlowInstanceStatus From, FlowInstanceStatus To)> AllowedTransitions = new()
    {
        (FlowInstanceStatus.Running, FlowInstanceStatus.Completed),
        (FlowInstanceStatus.Running, FlowInstanceStatus.Failed),
    };

    /// <summary>
    /// 检查状态转换是否合法
    /// </summary>
    public static bool CanTransition(FlowInstanceStatus from, FlowInstanceStatus to)
    {
        if (from == to) return true; // 同状态不算非法
        return AllowedTransitions.Contains((from, to));
    }

    /// <summary>
    /// 将字符串状态解析为枚举（兼容旧数据）
    /// </summary>
    public static FlowInstanceStatus Parse(string status)
    {
        return status?.ToLower() switch
        {
            "running" => FlowInstanceStatus.Running,
            "completed" => FlowInstanceStatus.Completed,
            "failed" => FlowInstanceStatus.Failed,
            _ => FlowInstanceStatus.Running // 默认
        };
    }

    /// <summary>
    /// 枚举转字符串（存入数据库）
    /// </summary>
    public static string ToString(FlowInstanceStatus status)
    {
        return status switch
        {
            FlowInstanceStatus.Running => "Running",
            FlowInstanceStatus.Completed => "Completed",
            FlowInstanceStatus.Failed => "Failed",
            _ => "Running"
        };
    }

    /// <summary>
    /// 是否为终态（不可再转换）
    /// </summary>
    public static bool IsTerminal(FlowInstanceStatus status)
    {
        return status is FlowInstanceStatus.Completed or FlowInstanceStatus.Failed;
    }
}
