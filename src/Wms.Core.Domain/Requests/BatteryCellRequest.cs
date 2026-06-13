namespace Wms.Core.Domain.Requests;

/// <summary>
/// 电芯请求（新建和编辑共用）
/// </summary>
public class BatteryCellRequest
{
    /// <summary>
    /// 物料ID
    /// </summary>
    public int? MaterialId { get; set; }

    /// <summary>
    /// 是否发送打包
    /// </summary>
    public int? IsSendPack { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    public string? Batch { get; set; }

    /// <summary>
    /// 条码
    /// </summary>
    public string? BarCode { get; set; }

    /// <summary>
    /// X等级
    /// </summary>
    public string? XLevel { get; set; }

    /// <summary>
    /// OCV3
    /// </summary>
    public decimal? OCV3 { get; set; }

    /// <summary>
    /// IR3
    /// </summary>
    public decimal? IR3 { get; set; }

    /// <summary>
    /// V3柯亚
    /// </summary>
    public decimal? V3KeYa { get; set; }

    /// <summary>
    /// OCV4
    /// </summary>
    public decimal? OCV4 { get; set; }

    /// <summary>
    /// IR4
    /// </summary>
    public decimal? IR4 { get; set; }

    /// <summary>
    /// V4柯亚
    /// </summary>
    public decimal? V4KeYa { get; set; }

    /// <summary>
    /// 容量
    /// </summary>
    public decimal? Capacity { get; set; }

    /// <summary>
    /// K值
    /// </summary>
    public decimal? KVal { get; set; }

    /// <summary>
    /// CCP
    /// </summary>
    public decimal? CCP { get; set; }

    /// <summary>
    /// Dcirnz
    /// </summary>
    public decimal? Dcirnz { get; set; }

    /// <summary>
    /// 序列号
    /// </summary>
    public string? Sequence { get; set; }

    /// <summary>
    /// 位置索引
    /// </summary>
    public int? LocIndex { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// 操作编号
    /// </summary>
    public int? OperationNumber { get; set; }

    /// <summary>
    /// 是否预先进
    /// </summary>
    public int? IsAdvance { get; set; }

    /// <summary>
    /// 容器编码
    /// </summary>
    public string? ContainerCode { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    public string? ModifiedBy { get; set; }
}
