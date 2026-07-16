namespace Wms.Core.Domain.ValueObjects;

/// <summary>
/// 工艺路线图内存结构（值对象）
/// 用于缓存和快速查询路线版本的步骤与转移关系
/// </summary>
public class ProcessRouteGraph
{
    /// <summary>
    /// 版本ID
    /// </summary>
    public int VersionId { get; set; }

    /// <summary>
    /// 步骤列表
    /// </summary>
    public List<ProcessRouteStepInfo> Steps { get; set; } = new();

    /// <summary>
    /// 转移列表
    /// </summary>
    public List<ProcessRouteTransitionInfo> Transitions { get; set; } = new();

    /// <summary>
    /// 查找起始步骤
    /// </summary>
    public ProcessRouteStepInfo? GetStartStep() => Steps.FirstOrDefault(s => s.IsStart);

    /// <summary>
    /// 查找终止步骤
    /// </summary>
    public ProcessRouteStepInfo? GetEndStep() => Steps.FirstOrDefault(s => s.IsEnd);

    /// <summary>
    /// 获取从指定步骤出发的所有转移（按 SortOrder 排序）
    /// </summary>
    public List<ProcessRouteTransitionInfo> GetOutgoingTransitions(int stepId)
        => Transitions.Where(t => t.FromStepId == stepId).OrderBy(t => t.SortOrder).ToList();

    /// <summary>
    /// 获取到达指定步骤的所有转移
    /// </summary>
    public List<ProcessRouteTransitionInfo> GetIncomingTransitions(int stepId)
        => Transitions.Where(t => t.ToStepId == stepId).OrderBy(t => t.SortOrder).ToList();

    /// <summary>
    /// 根据工序编码查找步骤
    /// </summary>
    public ProcessRouteStepInfo? FindStepByOperation(string operationCode)
        => Steps.FirstOrDefault(s => s.OperationCode == operationCode);

    /// <summary>
    /// 根据步骤ID查找步骤
    /// </summary>
    public ProcessRouteStepInfo? FindStep(int stepId)
        => Steps.FirstOrDefault(s => s.Id == stepId);

    /// <summary>
    /// 判断指定步骤是否有多个出边（分支节点）
    /// </summary>
    public bool IsBranchNode(int stepId)
        => Transitions.Count(t => t.FromStepId == stepId) > 1;
}

/// <summary>
/// 步骤信息（轻量级，用于图结构缓存）
/// </summary>
public class ProcessRouteStepInfo
{
    public int Id { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string StepType { get; set; } = "Normal";
    public bool IsStart { get; set; }
    public bool IsEnd { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 转移信息（轻量级，用于图结构缓存）
/// </summary>
public class ProcessRouteTransitionInfo
{
    public int Id { get; set; }
    public int FromStepId { get; set; }
    public int ToStepId { get; set; }
    public string TransitionType { get; set; } = "Sequential";
    public string? Label { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}
