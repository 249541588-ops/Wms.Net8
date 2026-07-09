using System.Text.RegularExpressions;

namespace Wms.Core.Infrastructure.Security;

/// <summary>
/// SQL 注入防御工具 - 提供 ORDER BY 列名/方向的本地白名单校验
/// </summary>
/// <remarks>
/// 适用范围：用户可控的 sortField / orderBy 字符串。
/// 白名单规则：仅允许 [A-Za-z_][A-Za-z0-9_]* 形式的标识符，可选 schema.table.column 链式。
/// 任何包含空格、逗号、括号、引号、T-SQL 关键字（SELECT/CASE/CAST/...）的输入都会被拒绝。
/// </remarks>
public static class SqlSafety
{
    /// <summary>
    /// 安全的 ORDER BY 列名白名单正则。
    /// 支持: column / table.column / schema.table.column
    /// </summary>
    public static readonly Regex SafeOrderByColumnRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$",
        RegexOptions.Compiled);

    /// <summary>
    /// 允许的排序方向（大小写不敏感）
    /// </summary>
    public static readonly HashSet<string> AllowedSortDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "ASC", "DESC"
    };

    /// <summary>
    /// 校验单个 ORDER BY 列名是否在白名单内。
    /// </summary>
    /// <param name="column">待校验的列名（不可包含方向、逗号、空格）</param>
    /// <returns>合法返回 true；否则 false</returns>
    public static bool IsValidOrderByColumn(string? column)
    {
        return !string.IsNullOrWhiteSpace(column) && SafeOrderByColumnRegex.IsMatch(column);
    }

    /// <summary>
    /// 校验排序方向是否合法（ASC/DESC，大小写不敏感）。
    /// </summary>
    public static bool IsValidSortDirection(string? direction)
    {
        return !string.IsNullOrEmpty(direction) && AllowedSortDirections.Contains(direction);
    }
}
