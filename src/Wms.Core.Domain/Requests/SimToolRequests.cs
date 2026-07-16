namespace Wms.Core.Application.DTOs;

/// <summary>
/// SIM 工具 — 修改工艺信息
/// </summary>
public class SimUpdateOperationRequest
{
    public string ContainerCode { get; set; } = string.Empty;
    public string? CurrentOperation { get; set; }
    public int? OperationNumber { get; set; }
    public int? IsAdvance { get; set; }
    public string? Batch { get; set; }
}

/// <summary>
/// SIM 工具 — 修改库位信息
/// </summary>
public class SimUpdateLocationRequest
{
    public string ContainerCode { get; set; } = string.Empty;
    public string OldLocationCode { get; set; } = string.Empty;
    public string NewLocationCode { get; set; } = string.Empty;
    public string? CurrentLocationTime { get; set; }
}

/// <summary>
/// SIM 工具 — 清理库位
/// </summary>
public class SimClearLocationRequest
{
    public string LocationCode { get; set; } = string.Empty;
}

/// <summary>
/// SIM 工具 — 创建货载
/// </summary>
public class SimCreateUnitloadRequest
{
    public string ContainerCode { get; set; } = string.Empty;
    public string CurrentOperation { get; set; } = string.Empty;
    public int MaterialId { get; set; }
    public int OperationNumber { get; set; } = 1;
    public int IsAdvance { get; set; } = 0;
    public int BatteryCount { get; set; } = 0;
}

/// <summary>
/// SIM 工具 — 拆盘
/// </summary>
public class SimSplitUnitloadRequest
{
    public string ContainerCode { get; set; } = string.Empty;
}

/// <summary>
/// SIM 工具 — 修改移动和分配标识
/// </summary>
public class SimUpdateMovementFlagsRequest
{
    public string ContainerCode { get; set; } = string.Empty;
    public bool? BeingMoved { get; set; }
    public bool? Allocated { get; set; }
}

/// <summary>
/// SIM 工具 — 叠盘
/// </summary>
public class SimMergeUnitloadRequest
{
    public string TargetContainerCode { get; set; } = string.Empty;
    public string SourceContainerCode { get; set; } = string.Empty;
}
