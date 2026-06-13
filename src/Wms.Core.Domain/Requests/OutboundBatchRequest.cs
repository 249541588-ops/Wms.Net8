using System.ComponentModel.DataAnnotations;

namespace Wms.Core.Domain.Requests;

/// <summary>
/// 出库批次请求
/// </summary>
public class OutboundBatchRequest
{
    /// <summary>
    /// 巷道ID
    /// </summary>
    public int LanewayId { get; set; }

    /// <summary>
    /// 物料ID
    /// </summary>
    public int MaterialId { get; set; }

    /// <summary>
    /// 当前操作
    /// </summary>
    [MaxLength(20)]
    public string? CurrentOperation { get; set; }

    /// <summary>
    /// 工艺次数
    /// </summary>
    public int? OperationNumber { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(20)]
    public string? Batch { get; set; }

    /// <summary>
    /// X等级
    /// </summary>
    [MaxLength(20)]
    public string? xLevel { get; set; }

    /// <summary>
    /// 需求数量
    /// </summary>
    public int QuantityRequired { get; set; } = 0;

    /// <summary>
    /// 已交付数量
    /// </summary>
    public int QuantityDelivered { get; set; } = 0;

    /// <summary>
    /// 是否提前
    /// </summary>
    public int IsAdvance { get; set; } = 0;

    /// <summary>
    /// 是否补料
    /// </summary>
    public int IsSupplement { get; set; } = 0;

    /// <summary>
    /// 状态
    /// </summary>
    public int Status { get; set; } = 0;

    /// <summary>
    /// 排序
    /// </summary>
    public int? Sort { get; set; }

    /// <summary>
    /// 错误计数
    /// </summary>
    public int ErrorCount { get; set; } = 0;

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(250)]
    public string? Comment { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    [MaxLength(64)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(64)]
    public string? ModifiedBy { get; set; }
}
