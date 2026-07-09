using System.Text.RegularExpressions;

namespace Wms.Core.Application.Services;

/// <summary>
/// SQL 安全校验器 — 用于自定义报表的 SQL 安全校验
/// </summary>
public partial class SqlValidator : ISqlValidator
{
    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE", "ALTER",
        "EXEC", "EXECUTE", "EXECUTE(", "SP_", "xp_", "CREATE",
        "GRANT", "REVOKE", "BACKUP", "RESTORE", "LOAD",
        "BCP", "BULK INSERT", "OPENROWSET", "OPENDATASOURCE",
        "LINKED SERVER", "SHUTDOWN", "KILL", "WAITFOR", "CURSOR"
    ];

    /// <summary>
    /// 校验 SQL 是否安全（仅允许 SELECT，禁止危险操作）
    /// </summary>
    public SqlValidationResult Validate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new SqlValidationResult(false, "SQL 不能为空");

        var trimmed = sql.Trim();

        // 必须以 SELECT 开头（忽略 WITH CTE 等）
        var upperSql = trimmed.ToUpperInvariant().TrimStart();
        if (!upperSql.StartsWith("SELECT") && !upperSql.StartsWith("WITH"))
        {
            return new SqlValidationResult(false, "仅允许 SELECT 查询语句");
        }

        // 检查危险关键字
        foreach (var keyword in ForbiddenKeywords)
        {
            // 使用正则匹配，确保是独立单词而非列名/表名的一部分
            var pattern = $@"\b{Regex.Escape(keyword)}\b";
            if (Regex.IsMatch(trimmed, pattern, RegexOptions.IgnoreCase))
            {
                return new SqlValidationResult(false, $"SQL 中包含禁止的关键字: {keyword}");
            }
        }

        // 检查分号（防止多条语句）
        if (trimmed.Count(c => c == ';') > 0)
        {
            return new SqlValidationResult(false, "不允许包含多条 SQL 语句（分号）");
        }

        // 检查注释符注入
        if (trimmed.Contains("--") || trimmed.Contains("/*"))
        {
            return new SqlValidationResult(false, "不允许包含 SQL 注释符（-- 或 /*）");
        }

        return new SqlValidationResult(true, null);
    }
}
