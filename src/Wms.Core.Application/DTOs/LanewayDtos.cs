namespace Wms.Core.Application.DTOs;

/// <summary>
/// 创建巷道请求
/// </summary>
public record CreateLanewayRequest
{
    /// <summary>
    /// 通道编码
    /// </summary>
    public string? LanewayCode { get; init; }

    /// <summary>
    /// 仓库编号
    /// </summary>
    public int? WarehouseId { get; init; }

    /// <summary>
    /// 区域
    /// </summary>
    public string? Area { get; init; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// 是否自动化
    /// </summary>
    public bool? Automated { get; init; }

    /// <summary>
    /// 是否双深
    /// </summary>
    public bool? DoubleDeep { get; init; }

    /// <summary>
    /// 预留库位数量
    /// </summary>
    public int? ReservedLocationCount { get; init; }

    /// <summary>
    /// 创建用户
    /// </summary>
    public string? CreatedBy { get; init; }
}

/// <summary>
/// 更新巷道请求
/// </summary>
public record UpdateLanewayRequest
{
    /// <summary>
    /// 通道编码
    /// </summary>
    public string? LanewayCode { get; init; }

    /// <summary>
    /// 仓库编号
    /// </summary>
    public int? WarehouseId { get; init; }

    /// <summary>
    /// 区域
    /// </summary>
    public string? Area { get; init; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// 是否自动化
    /// </summary>
    public bool? Automated { get; init; }

    /// <summary>
    /// 是否离线
    /// </summary>
    public bool? Offline { get; init; }

    /// <summary>
    /// 离线备注
    /// </summary>
    public string? OfflineComment { get; init; }

    /// <summary>
    /// 是否双深
    /// </summary>
    public bool? DoubleDeep { get; init; }

    /// <summary>
    /// 预留库位数量
    /// </summary>
    public int? ReservedLocationCount { get; init; }

    /// <summary>
    /// 修改用户
    /// </summary>
    public string? ModifiedBy { get; init; }
}
