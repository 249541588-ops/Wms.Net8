namespace Wms.Core.Domain.Requests;

/// <summary>
/// 电芯分选请求（新建和编辑共用）
/// </summary>
public class BatteryCellSortingRequest
{
    /// <summary>
    /// 物料ID
    /// </summary>
    public int? MaterialId { get; set; }

    /// <summary>
    /// 挑选名称
    /// </summary>
    public string? PickName { get; set; }

    /// <summary>
    /// 挑选ID
    /// </summary>
    public string? PickId { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    public string? XSpecification { get; set; }

    /// <summary>
    /// 容量最小值
    /// </summary>
    public decimal? CapacityMin { get; set; }

    /// <summary>
    /// 容量最大值
    /// </summary>
    public decimal? CapacityMax { get; set; }

    /// <summary>
    /// OCV4最小值
    /// </summary>
    public decimal? OCV4Min { get; set; }

    /// <summary>
    /// OCV4最大值
    /// </summary>
    public decimal? OCV4Max { get; set; }

    /// <summary>
    /// IR4最小值
    /// </summary>
    public decimal? IR4Min { get; set; }

    /// <summary>
    /// IR4最大值
    /// </summary>
    public decimal? IR4Max { get; set; }

    /// <summary>
    /// K值最小值
    /// </summary>
    public decimal? KValMin { get; set; }

    /// <summary>
    /// K值最大值
    /// </summary>
    public decimal? KValMax { get; set; }

    /// <summary>
    /// Dcirnz最小值
    /// </summary>
    public decimal? DcirnzMin { get; set; }

    /// <summary>
    /// Dcirnz最大值
    /// </summary>
    public decimal? DcirnzMax { get; set; }

    /// <summary>
    /// 通道
    /// </summary>
    public string? Passageway { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public short? IsEnable { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    public string? ModifiedBy { get; set; }
}
