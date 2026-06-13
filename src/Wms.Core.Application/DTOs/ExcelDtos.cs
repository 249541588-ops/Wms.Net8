namespace Wms.Core.Application.DTOs;

/// <summary>
/// 物料导入 DTO
/// </summary>
public record MaterialImportDto
{
    /// <summary>
    /// 物料编码
    /// </summary>
    public string MaterialCode { get; init; } = string.Empty;

    /// <summary>
    /// 说明
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 物料类型
    /// </summary>
    public string? MaterialType { get; init; }

    /// <summary>
    /// 规格
    /// </summary>
    public string? Specification { get; init; }

    /// <summary>
    /// 单位
    /// </summary>
    public string Uom { get; init; } = "PCS";

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 行号（用于错误提示）
    /// </summary>
    public int RowNumber { get; init; }
}

/// <summary>
/// 库存导出 DTO
/// </summary>
public record StockExportDto
{
    /// <summary>
    /// 物料编码
    /// </summary>
    public string MaterialCode { get; init; } = string.Empty;

    /// <summary>
    /// 物料说明
    /// </summary>
    public string MaterialDescription { get; init; } = string.Empty;

    /// <summary>
    /// 批次
    /// </summary>
    public string Batch { get; init; } = string.Empty;

    /// <summary>
    /// 库存状态
    /// </summary>
    public string StockStatus { get; init; } = string.Empty;

    /// <summary>
    /// 数量
    /// </summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// 可用数量
    /// </summary>
    public decimal QuantityAvailable { get; init; }

    /// <summary>
    /// 单位
    /// </summary>
    public string Uom { get; init; } = string.Empty;

    /// <summary>
    /// 位置编码
    /// </summary>
    public string? LocationCode { get; init; }
}
