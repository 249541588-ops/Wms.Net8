namespace Wms.Core.Domain.Requests;

/// <summary>
/// 创建废料批次设置请求
/// </summary>
public record CreateWasteBatchSettingRequest
{
    public bool? IsBuiltIn { get; init; }
    public int? LocationType { get; init; }
    public string? LocationCode { get; init; }
    public string? ContainerCode { get; init; }
    public string? Batch { get; init; }
}

/// <summary>
/// 更新废料批次设置请求
/// </summary>
public record UpdateWasteBatchSettingRequest
{
    public bool? IsBuiltIn { get; init; }
    public int? LocationType { get; init; }
    public string? LocationCode { get; init; }
    public string? ContainerCode { get; init; }
    public string? Batch { get; init; }
}
