namespace Wms.Core.Application.DTOs;

/// <summary>
/// 创建库位请求
/// </summary>
public record CreateLocationRequest
{
    /// <summary>
    /// 库位编码
    /// </summary>
    public string? LocationCode { get; init; }

    /// <summary>
    /// 库位类型
    /// </summary>
    public string? LocationType { get; init; }

    /// <summary>
    /// 货架编号
    /// </summary>
    public int? RackId { get; init; }

    /// <summary>
    /// 仓库编号
    /// </summary>
    public int? WarehouseId { get; init; }

    /// <summary>
    /// 列号
    /// </summary>
    public int? xColumn { get; init; }

    /// <summary>
    /// 层号
    /// </summary>
    public int? xLevel { get; init; }

    /// <summary>
    /// 入库上限
    /// </summary>
    public int? InboundLimit { get; init; }

    /// <summary>
    /// 出库上限
    /// </summary>
    public int? OutboundLimit { get; init; }

    /// <summary>
    /// 重量上限
    /// </summary>
    public decimal? WeightLimit { get; init; }

    /// <summary>
    /// 高度上限
    /// </summary>
    public decimal? HeightLimit { get; init; }

    /// <summary>
    /// 存储组
    /// </summary>
    public string? StorageGroup { get; init; }

    /// <summary>
    /// 子存储组
    /// </summary>
    public string? SubStorageGroup { get; init; }

    /// <summary>
    /// 请求类型
    /// </summary>
    public string? RequestType { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// 区域名称
    /// </summary>
    public string? AreaName { get; init; }

    /// <summary>
    /// 通道编码集合
    /// </summary>
    public string? LanewayCodes { get; init; }

    /// <summary>
    /// 规格
    /// </summary>
    public string? xSpecification { get; init; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// 创建用户
    /// </summary>
    public string? CreatedBy { get; init; }
}

/// <summary>
/// 更新库位请求
/// </summary>
public record UpdateLocationRequest
{
    /// <summary>
    /// 库位编码
    /// </summary>
    public string? LocationCode { get; init; }

    /// <summary>
    /// 库位类型
    /// </summary>
    public string? LocationType { get; init; }

    /// <summary>
    /// 货架编号
    /// </summary>
    public int? RackId { get; init; }

    /// <summary>
    /// 仓库编号
    /// </summary>
    public int? WarehouseId { get; init; }

    /// <summary>
    /// 列号
    /// </summary>
    public int? xColumn { get; init; }

    /// <summary>
    /// 层号
    /// </summary>
    public int? xLevel { get; init; }

    /// <summary>
    /// 入库上限
    /// </summary>
    public int? InboundLimit { get; init; }

    /// <summary>
    /// 出库上限
    /// </summary>
    public int? OutboundLimit { get; init; }

    /// <summary>
    /// 是否禁止入库
    /// </summary>
    public bool? InboundDisabled { get; init; }

    /// <summary>
    /// 禁止入库备注
    /// </summary>
    public string? InboundDisabledComment { get; init; }

    /// <summary>
    /// 是否禁止出库
    /// </summary>
    public bool? OutboundDisabled { get; init; }

    /// <summary>
    /// 禁止出库备注
    /// </summary>
    public string? OutboundDisabledComment { get; init; }

    /// <summary>
    /// 重量上限
    /// </summary>
    public decimal? WeightLimit { get; init; }

    /// <summary>
    /// 高度上限
    /// </summary>
    public decimal? HeightLimit { get; init; }

    /// <summary>
    /// 存储组
    /// </summary>
    public string? StorageGroup { get; init; }

    /// <summary>
    /// 子存储组
    /// </summary>
    public string? SubStorageGroup { get; init; }

    /// <summary>
    /// 请求类型
    /// </summary>
    public string? RequestType { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// 区域名称
    /// </summary>
    public string? AreaName { get; init; }

    /// <summary>
    /// 通道编码集合
    /// </summary>
    public string? LanewayCodes { get; init; }

    /// <summary>
    /// 规格
    /// </summary>
    public string? xSpecification { get; init; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// 修改用户
    /// </summary>
    public string? ModifiedBy { get; init; }
}

/// <summary>
/// 库位网格单元 DTO
/// </summary>
public class LocationCellDto
{
    public int LocationId { get; set; }
    public string? LocationCode { get; set; }
    public int xLevel { get; set; }
    public int xColumn { get; set; }
    public bool xExists { get; set; }
    public int UnitloadCount { get; set; }
    public bool InboundDisabled { get; set; }
    public bool OutboundDisabled { get; set; }
    public int InboundCount { get; set; }
    public int OutboundCount { get; set; }
    public int InboundLimit { get; set; }
    public int OutboundLimit { get; set; }
    public string? StorageGroup { get; set; }
    public string? SubStorageGroup { get; set; }
}

/// <summary>
/// 批量更新库位请求
/// </summary>
public class BatchUpdateLocationRequest
{
    public int[] LocationIds { get; set; } = [];
    public bool? InboundDisabled { get; set; }
    public bool? OutboundDisabled { get; set; }
    public bool? xExists { get; set; }
    public int? UnitloadCount { get; set; }
    public string? StorageGroup { get; set; }
    public string? SubStorageGroup { get; set; }
}
