namespace Wms.Core.Application.DTOs;

/// <summary>
/// 创建货架请求
/// </summary>
public record CreateRackRequest
{
    /// <summary>
    /// 货架编码
    /// </summary>
    public string? RackCode { get; init; }

    /// <summary>
    /// 仓库编号
    /// </summary>
    public int? WarehouseId { get; init; }

    /// <summary>
    /// 通道编号
    /// </summary>
    public int? LanewayId { get; init; }

    /// <summary>
    /// 侧面
    /// </summary>
    public int? Side { get; init; }

    /// <summary>
    /// 深度
    /// </summary>
    public int? Deep { get; init; }

    /// <summary>
    /// 列数
    /// </summary>
    public int? Columns { get; init; }

    /// <summary>
    /// 层数
    /// </summary>
    public int? Levels { get; init; }

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
/// 更新货架请求
/// </summary>
public record UpdateRackRequest
{
    /// <summary>
    /// 货架编码
    /// </summary>
    public string? RackCode { get; init; }

    /// <summary>
    /// 仓库编号
    /// </summary>
    public int? WarehouseId { get; init; }

    /// <summary>
    /// 通道编号
    /// </summary>
    public int? LanewayId { get; init; }

    /// <summary>
    /// 侧面
    /// </summary>
    public int? Side { get; init; }

    /// <summary>
    /// 深度
    /// </summary>
    public int? Deep { get; init; }

    /// <summary>
    /// 列数
    /// </summary>
    public int? Columns { get; init; }

    /// <summary>
    /// 层数
    /// </summary>
    public int? Levels { get; init; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// 修改用户
    /// </summary>
    public string? ModifiedBy { get; init; }
}
