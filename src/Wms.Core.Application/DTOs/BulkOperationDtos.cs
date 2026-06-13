namespace Wms.Core.Application.DTOs;

/// <summary>
/// 批量操作结果 DTO
/// </summary>
public class BulkOperationResultDto
{
    /// <summary>
    /// 总数
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 成功数
    /// </summary>
    public int Success { get; set; }

    /// <summary>
    /// 失败数
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// 错误详情列表
    /// </summary>
    public List<BulkErrorDto> Errors { get; set; } = new();
}

/// <summary>
/// 批量错误详情
/// </summary>
public class BulkErrorDto
{
    /// <summary>
    /// 索引
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 标识符
    /// </summary>
    public string? Identifier { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 错误代码
    /// </summary>
    public string? ErrorCode { get; set; }
}

/// <summary>
/// 创建物料批量请求 DTO
/// </summary>
public class BulkCreateMaterialDto
{
    /// <summary>
    /// 物料列表
    /// </summary>
    public List<CreateMaterialItemDto> Materials { get; set; } = new();
}

/// <summary>
/// 创建物料项 DTO
/// </summary>
public class CreateMaterialItemDto
{
    /// <summary>
    /// 物料编码
    /// </summary>
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>
    /// 说明
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 物料类型
    /// </summary>
    public string? MaterialType { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 单位
    /// </summary>
    public string Uom { get; set; } = "PCS";

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 批量更新物料请求 DTO
/// </summary>
public class BulkUpdateMaterialDto
{
    /// <summary>
    /// 物料 ID 列表
    /// </summary>
    public List<int> Ids { get; set; } = new();

    /// <summary>
    /// 要更新的字段
    /// </summary>
    public Dictionary<string, object?> Updates { get; set; } = new();
}

/// <summary>
/// 批量删除请求 DTO
/// </summary>
public class BulkDeleteRequestDto
{
    /// <summary>
    /// ID 列表
    /// </summary>
    public List<int> Ids { get; set; } = new();
}
