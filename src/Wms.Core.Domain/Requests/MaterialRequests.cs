namespace Wms.Core.Domain.Requests;

/// <summary>
/// 物料请求（新建和编辑共用）
/// </summary>
public class MaterialRequest
{
    /// <summary>
    /// 物料ID（编辑时传入）
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// 物料编码
    /// </summary>
    public string? MaterialCode { get; set; }

    /// <summary>
    /// 物料类型
    /// </summary>
    public string? MaterialType { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 备用编码
    /// </summary>
    public string? SpareCode { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 助记码
    /// </summary>
    public string? MnemonicCode { get; set; }

    /// <summary>
    /// 是否启用批次管理
    /// </summary>
    public bool? BatchEnabled { get; set; }

    /// <summary>
    /// 物料组
    /// </summary>
    public string? MaterialGroup { get; set; }

    /// <summary>
    /// 有效天数
    /// </summary>
    public decimal? ValidDays { get; set; }

    /// <summary>
    /// 停留时间
    /// </summary>
    public decimal? StandingTime { get; set; }

    /// <summary>
    /// ABC分类
    /// </summary>
    public string? AbcClass { get; set; }

    /// <summary>
    /// 计量单位
    /// </summary>
    public string? Uom { get; set; }

    /// <summary>
    /// 默认存储组
    /// </summary>
    public string? DefaultStorageGroup { get; set; }

    /// <summary>
    /// 条码
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// 单位体积
    /// </summary>
    public decimal? UnitVolume { get; set; }

    /// <summary>
    /// 单位长度
    /// </summary>
    public decimal? UnitLength { get; set; }

    /// <summary>
    /// 单位宽度
    /// </summary>
    public decimal? UnitWidth { get; set; }

    /// <summary>
    /// 单位高度
    /// </summary>
    public decimal? UnitHeight { get; set; }

    /// <summary>
    /// 单位重量
    /// </summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>
    /// 下限
    /// </summary>
    public decimal? LowerBound { get; set; }

    /// <summary>
    /// 上限
    /// </summary>
    public decimal? UpperBound { get; set; }

    /// <summary>
    /// 默认数量
    /// </summary>
    public decimal? DefaultQuantity { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    public string? CreatedBy { get; set; }
}
