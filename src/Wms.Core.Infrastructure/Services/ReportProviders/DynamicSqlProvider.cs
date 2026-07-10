using System.Text;
using System.Text.Json;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Services;
using Wms.Core.Domain.Entities.System;

namespace Wms.Core.Infrastructure.Services.ReportProviders;

/// <summary>
/// 自定义查询 Provider — 处理 Custom 类型报表的 SQL 模板执行
/// </summary>
public class DynamicSqlProvider
{
    private readonly ISqlValidator _sqlValidator;

    public DynamicSqlProvider(ISqlValidator sqlValidator)
    {
        _sqlValidator = sqlValidator;
    }

    /// <summary>
    /// 构建数据查询 SQL（替换模板中的参数占位符，拼接筛选条件）
    /// </summary>
    public string BuildDataSql(
        ReportConfig config,
        Dictionary<string, object?> filters,
        List<string> columns,
        out Dictionary<string, object> sqlParams)
    {
        if (string.IsNullOrWhiteSpace(config.SqlTemplate))
            throw new InvalidOperationException($"报表 {config.ReportCode} 的 SQL 模板为空");

        var validation = _sqlValidator.Validate(config.SqlTemplate);
        if (!validation.IsValid)
            throw new InvalidOperationException($"报表 {config.ReportCode} 的 SQL 校验失败: {validation.ErrorMessage}");

        sqlParams = new Dictionary<string, object>();
        var whereClauses = new List<string>();

        // 解析 FilterSqlMapping，将筛选条件映射为 WHERE 子句
        if (!string.IsNullOrEmpty(config.FilterSqlMapping))
        {
            var mappings = JsonSerializer.Deserialize<List<FilterSqlMappingItem>>(config.FilterSqlMapping);
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    var key = filters.Keys.FirstOrDefault(k =>
                        string.Equals(k, mapping.FilterField, StringComparison.OrdinalIgnoreCase));
                    if (key != null && filters.TryGetValue(key, out var rawValue) && rawValue != null)
                    {
                        var value = ReportFilterHelper.UnwrapValue(rawValue);
                        if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        {
                            whereClauses.Add(mapping.SqlExpression);
                            sqlParams[$"@{mapping.FilterField}"] = value;
                        }
                    }
                }
            }
        }

        // 构建 SQL：模板 + WHERE + 列过滤（如果模板包含 {columns} 占位符则替换）
        var sql = config.SqlTemplate;

        // 替换 {columns} 占位符（如果存在）
        if (sql.Contains("{columns}") && columns.Count > 0)
        {
            sql = sql.Replace("{columns}", string.Join(", ", columns));
        }

        // 追加筛选条件
        if (whereClauses.Count > 0)
        {
            var whereStr = string.Join(" AND ", whereClauses);
            // 智能插入 WHERE：如果 SQL 已包含 WHERE 则用 AND 拼接
            if (sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) < 0)
            {
                // 在 ORDER BY 或 GROUP BY 之前插入 WHERE
                var insertPos = FindInsertPosition(sql);
                sql = sql.Insert(insertPos, $" WHERE {whereStr}");
            }
            else
            {
                // 已有 WHERE 子句，追加 AND
                var whereIdx = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) + 5;
                sql = sql.Insert(whereIdx, $" ({whereStr}) AND");
            }
        }

        return sql;
    }

    /// <summary>
    /// 构建 COUNT SQL
    /// </summary>
    public string BuildCountSql(ReportConfig config, Dictionary<string, object?> filters, out Dictionary<string, object> sqlParams)
    {
        if (string.IsNullOrWhiteSpace(config.CountSqlTemplate))
            throw new InvalidOperationException($"报表 {config.ReportCode} 的 COUNT SQL 模板为空");

        var validation = _sqlValidator.Validate(config.CountSqlTemplate);
        if (!validation.IsValid)
            throw new InvalidOperationException($"报表 {config.ReportCode} 的 COUNT SQL 校验失败: {validation.ErrorMessage}");

        sqlParams = new Dictionary<string, object>();
        var whereClauses = new List<string>();

        if (!string.IsNullOrEmpty(config.FilterSqlMapping))
        {
            var mappings = JsonSerializer.Deserialize<List<FilterSqlMappingItem>>(config.FilterSqlMapping);
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    var key = filters.Keys.FirstOrDefault(k =>
                        string.Equals(k, mapping.FilterField, StringComparison.OrdinalIgnoreCase));
                    if (key != null && filters.TryGetValue(key, out var rawValue) && rawValue != null)
                    {
                        var value = ReportFilterHelper.UnwrapValue(rawValue);
                        if (value != null && !string.IsNullOrEmpty(value.ToString()))
                        {
                            whereClauses.Add(mapping.SqlExpression);
                            sqlParams[$"@{mapping.FilterField}"] = value;
                        }
                    }
                }
            }
        }

        var sql = config.CountSqlTemplate;

        if (whereClauses.Count > 0)
        {
            var whereStr = string.Join(" AND ", whereClauses);
            if (sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) < 0)
            {
                var insertPos = FindInsertPosition(sql);
                sql = sql.Insert(insertPos, $" WHERE {whereStr}");
            }
            else
            {
                var whereIdx = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) + 5;
                sql = sql.Insert(whereIdx, $" ({whereStr}) AND");
            }
        }

        return sql;
    }

    /// <summary>
    /// 找到合适的 WHERE 插入位置（在 ORDER BY / GROUP BY / HAVING 之前，或末尾）
    /// </summary>
    private static int FindInsertPosition(string sql)
    {
        var upperSql = sql.ToUpperInvariant();

        var keywords = new[] { "ORDER BY", "GROUP BY", "HAVING" };
        int minPos = sql.Length;

        foreach (var keyword in keywords)
        {
            var idx = upperSql.IndexOf(keyword);
            if (idx >= 0 && idx < minPos)
                minPos = idx;
        }

        // 跳过开头的 SELECT 部分，至少在第一个 FROM 之后
        var fromIdx = upperSql.IndexOf("FROM");
        if (fromIdx >= 0 && minPos <= fromIdx)
            minPos = sql.Length;

        return minPos;
    }
}
