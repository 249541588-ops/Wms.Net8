namespace Wms.Core.Application.DTOs;

/// <summary>
/// 分支选项 DTO
/// </summary>
public class BranchOptionDto
{
    public int TransitionId { get; set; }
    public int FromStepId { get; set; }
    public int ToStepId { get; set; }
    public string ToStepName { get; set; } = "";
    public string ToOperationCode { get; set; } = "";
    public string? Label { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// 路线图 DTO（G6 格式）
/// </summary>
public class RouteGraphDto
{
    public int VersionId { get; set; }
    public string RouteName { get; set; } = "";
    public int Version { get; set; }
    public List<GraphNodeDto> Nodes { get; set; } = new();
    public List<GraphEdgeDto> Edges { get; set; } = new();
}

/// <summary>
/// 图节点 DTO
/// </summary>
public class GraphNodeDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string OperationCode { get; set; } = "";
    public string StepType { get; set; } = "";
    public bool IsStart { get; set; }
    public bool IsEnd { get; set; }
    public int SortOrder { get; set; }
    public bool IsCurrent { get; set; }
}

/// <summary>
/// 图边 DTO
/// </summary>
public class GraphEdgeDto
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Label { get; set; }
    public string TransitionType { get; set; } = "";
    public bool IsDefault { get; set; }
}

/// <summary>
/// 托盘工艺状态 DTO（不含历史轨迹，历史通过分页接口获取）
/// </summary>
public class UnitloadTrackStatusDto
{
    public int UnitloadId { get; set; }
    public string? ContainerCode { get; set; }
    public string? CurrentOperation { get; set; }
    public string? NextOperation { get; set; }
    public bool IsAwaitingBranchSelection { get; set; }
    public string? RouteName { get; set; }
    public int? Version { get; set; }
    public int? CurrentStepId { get; set; }
    public List<BranchOptionDto>? NextOptions { get; set; }
}

/// <summary>
/// 轨迹条目 DTO
/// </summary>
public class TrackEntryDto
{
    public int Id { get; set; }
    public string? OperationCode { get; set; }
    public string ActionType { get; set; } = "";
    public string? FromOperation { get; set; }
    public string? ToOperation { get; set; }
    public string? Operator { get; set; }
    public DateTime? CreatedTime { get; set; }
}

/// <summary>
/// 物料绑定 DTO
/// </summary>
public class MaterialBindingDto
{
    public int Id { get; set; }
    public int ProcessRouteId { get; set; }
    public int MaterialId { get; set; }
    public string? MaterialCode { get; set; }
    public string? MaterialType { get; set; }
    public string? Description { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// 创建物料绑定 DTO
/// </summary>
public class CreateMaterialBindingDto
{
    public int MaterialId { get; set; }
    public int Priority { get; set; }
}
