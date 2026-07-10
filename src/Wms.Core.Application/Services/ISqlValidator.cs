namespace Wms.Core.Application.Services;

/// <summary>
/// SQL 安全校验接口 — 用于自定义报表的 SQL 校验
/// </summary>
public interface ISqlValidator
{
    /// <summary>
    /// 校验 SQL 是否安全（仅允许 SELECT，禁止危险操作）
    /// </summary>
    SqlValidationResult Validate(string sql);
}

/// <summary>
/// SQL 校验结果
/// </summary>
public record SqlValidationResult(bool IsValid, string? ErrorMessage);
