using System.Text.Json;
using Dapper;
using Hangfire;
using BackgroundJobClient = Hangfire.BackgroundJob;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Services;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Domain.ValueObjects;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Security;
using Wms.Core.Infrastructure.Services.ReportProviders;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 报表服务实现 — 核心查询、用户配置管理、导出任务提交
/// </summary>
public class ReportService : IReportService
{
    private readonly WmsDbContext _db;
    private readonly IEnumerable<IReportQueryProvider> _providers;
    private readonly DynamicSqlProvider _dynamicSqlProvider;
    private readonly ILogger<ReportService> _logger;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly string _connectionString;

    public ReportService(
        WmsDbContext db,
        IEnumerable<IReportQueryProvider> providers,
        DynamicSqlProvider dynamicSqlProvider,
        ILogger<ReportService> logger,
        IConfiguration config,
        IMemoryCache cache)
    {
        _db = db;
        _providers = providers;
        _dynamicSqlProvider = dynamicSqlProvider;
        _logger = logger;
        _config = config;
        _cache = cache;
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection 连接字符串未配置");
    }

    #region 报表列表与配置

    public async Task<List<ReportConfigListDto>> GetReportListAsync()
    {
        return await _db.ReportConfigs
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Category)
            .ThenBy(r => r.ReportName)
            .Select(r => new ReportConfigListDto(
                r.Id,
                r.ReportCode ?? "",
                r.ReportName ?? "",
                r.Category,
                r.Description,
                r.ReportType
            ))
            .ToListAsync();
    }

    public async Task<ReportConfigDto?> GetReportConfigAsync(string reportCode)
    {
        var config = await _db.ReportConfigs
            .FirstOrDefaultAsync(r => r.ReportCode == reportCode && r.IsEnabled);

        if (config == null) return null;

        var dto = ToReportConfigDto(config);

        // 合并 Provider 提供的筛选选项（带缓存）
        var provider = _providers.FirstOrDefault(p => p.ReportCode == reportCode);
        if (provider != null)
        {
            var filterOptions = _cache.GetOrCreate($"ReportFilterOptions:{reportCode}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheKeyTypes.TestCacheKeyTime);
                return provider.GetFilterOptions();
            });

            if (filterOptions.Count > 0)
            {
                dto = dto with
                {
                    AvailableFilters = dto.AvailableFilters.Select(f =>
                        filterOptions.TryGetValue(f.Field, out var opts) && opts.Count > 0
                            ? f with { Options = opts }
                            : f
                    ).ToList()
                };
            }
        }

        return dto;
    }

    #endregion

    #region 数据查询

    public async Task<ReportQueryResult> QueryReportDataAsync(ReportQueryRequest request)
    {
        var config = await _db.ReportConfigs
            .FirstOrDefaultAsync(r => r.ReportCode == request.ReportCode && r.IsEnabled)
            ?? throw new InvalidOperationException($"报表 {request.ReportCode} 不存在或未启用");

        string dataSql;
        string countSql;
        Dictionary<string, object> dataParams;
        Dictionary<string, object> countParams;

        if (config.ReportType == "Custom")
        {
            // 自定义报表：使用 DynamicSqlProvider
            var columns = request.Filters.ContainsKey("_columns")
                ? JsonSerializer.Deserialize<List<string>>(request.Filters["_columns"]?.ToString() ?? "[]") ?? []
                : JsonSerializer.Deserialize<List<string>>(config.DefaultColumns ?? "[]") ?? [];

            dataSql = _dynamicSqlProvider.BuildDataSql(config, request.Filters, columns, out dataParams);
            countSql = _dynamicSqlProvider.BuildCountSql(config, request.Filters, out countParams);
        }
        else
        {
            // 预置报表：使用对应的 Provider
            var provider = _providers.FirstOrDefault(p => p.ReportCode == request.ReportCode)
                ?? throw new InvalidOperationException($"报表 {request.ReportCode} 的查询 Provider 未注册");

            var columns = JsonSerializer.Deserialize<List<string>>(config.DefaultColumns ?? "[]") ?? [];
            dataSql = provider.BuildDataSql(request.Filters, columns, out dataParams);
            countSql = provider.BuildCountSql(request.Filters, out countParams);
        }

        // 添加分页参数
        dataParams["@skip"] = (request.PageNumber - 1) * request.PageSize;
        dataParams["@take"] = request.PageSize;

        // 添加排序
        var sort = NormalizeSort(request.SortField, request.SortDirection, config.DefaultSort);
        dataSql = AppendOrderBy(dataSql, sort);

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // 执行 COUNT 查询
        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, countParams);

        // 执行数据查询（SQL Server 分页语法）
        var pagedSql = AppendSqlServerPaging(dataSql);
        var rows = (await conn.QueryAsync<dynamic>(pagedSql, dataParams)).ToList();

        // 转换为 Dictionary
        var columnsList = ExtractColumnNames(rows);
        var data = rows.Select(row => ((IDictionary<string, object>)row)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value as object)).ToList();

        return new ReportQueryResult(totalCount, request.PageNumber, request.PageSize, columnsList, data);
    }

    #endregion

    #region 用户配置管理

    public async Task<List<UserReportConfigDto>> GetUserConfigsAsync(string reportCode, int userId)
    {
        var entities = await _db.UserReportConfigs
            .Where(c => c.ReportCode == reportCode && c.UserId == userId)
            .OrderByDescending(c => c.IsDefault)
            .ThenByDescending(c => c.ModifiedTime)
            .ToListAsync();

        return entities.Select(c => new UserReportConfigDto(
            c.Id,
            c.ConfigName ?? "",
            JsonSerializer.Deserialize<List<string>>(c.SelectedColumns ?? "[]") ?? [],
            JsonSerializer.Deserialize<List<string>>(c.ColumnOrder ?? "[]") ?? [],
            JsonSerializer.Deserialize<Dictionary<string, int>>(c.ColumnWidths ?? "{}"),
            c.IsDefault
        )).ToList();
    }

    public async Task<UserReportConfigDto> SaveUserConfigAsync(string reportCode, int userId, string userName, SaveUserConfigRequest request)
    {
        // 如果设为默认，取消该用户其他默认配置
        if (request.IsDefault)
        {
            var existingDefaults = await _db.UserReportConfigs
                .Where(c => c.ReportCode == reportCode && c.UserId == userId && c.IsDefault)
                .ToListAsync();
            foreach (var d in existingDefaults)
                d.IsDefault = false;
        }

        var entity = new UserReportConfig
        {
            UserId = userId,
            UserName = userName,
            ReportCode = reportCode,
            ConfigName = request.ConfigName,
            SelectedColumns = JsonSerializer.Serialize(request.SelectedColumns),
            ColumnOrder = JsonSerializer.Serialize(request.ColumnOrder),
            ColumnWidths = request.ColumnWidths != null ? JsonSerializer.Serialize(request.ColumnWidths) : null,
            IsDefault = request.IsDefault,
            CreatedTime = DateTime.Now,
            ModifiedTime = DateTime.Now,
        };

        _db.UserReportConfigs.Add(entity);
        await _db.SaveChangesAsync();

        return new UserReportConfigDto(
            entity.Id,
            entity.ConfigName ?? "",
            request.SelectedColumns,
            request.ColumnOrder,
            request.ColumnWidths,
            entity.IsDefault
        );
    }

    public async Task DeleteUserConfigAsync(int configId)
    {
        var entity = await _db.UserReportConfigs.FindAsync(configId);
        if (entity != null)
        {
            _db.UserReportConfigs.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }

    #endregion

    #region 导出任务管理

    public async Task<ReportExportTaskDto> SubmitExportAsync(string reportCode, int userId, string userName, ReportExportRequest request)
    {
        var config = await _db.ReportConfigs
            .FirstOrDefaultAsync(r => r.ReportCode == reportCode && r.IsEnabled)
            ?? throw new InvalidOperationException($"报表 {reportCode} 不存在或未启用");

        var exportDir = Path.GetFullPath(_config["ReportSettings:ExportDirectory"] ?? "./exports");
        Directory.CreateDirectory(exportDir);

        var taskId = Guid.NewGuid().ToString("N");
        var fileName = $"{config.ReportName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        var task = new ReportExportTask
        {
            TaskId = taskId,
            ReportCode = reportCode,
            UserId = userId,
            UserName = userName,
            Status = "Pending",
            FilterParams = JsonSerializer.Serialize(request.Filters),
            ColumnConfig = request.Columns != null ? JsonSerializer.Serialize(request.Columns) : null,
            FileName = fileName,
            FilePath = Path.Combine(exportDir, fileName),
            CreatedTime = DateTime.Now,
        };

        _db.ReportExportTasks.Add(task);
        await _db.SaveChangesAsync();

        // 提交 Hangfire 后台任务
        Hangfire.BackgroundJob.Enqueue<IReportExportService>(x =>
            x.ExecuteExportAsync(taskId, reportCode, request.Filters, request.Columns, task.FilePath!));

        _logger.LogInformation("报表导出任务已提交: TaskId={TaskId}, ReportCode={ReportCode}", taskId, reportCode);

        return ToExportTaskDto(task);
    }

    public async Task<List<ReportExportTaskDto>> GetExportTasksAsync(int userId)
    {
        return await _db.ReportExportTasks
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedTime)
            .Take(50)
            .Select(t => new ReportExportTaskDto(
                t.TaskId ?? "",
                t.ReportCode ?? "",
                t.Status ?? "Unknown",
                t.FileName,
                t.FileSize,
                t.TotalRows,
                t.CompletedAt,
                t.ErrorMessage
            ))
            .ToListAsync();
    }

    #endregion

    #region 自定义报表管理

    public async Task<ReportConfigDto> CreateCustomReportAsync(int userId, string userName, CreateCustomReportRequest request)
    {
        var entity = new ReportConfig
        {
            ReportCode = request.ReportCode,
            ReportName = request.ReportName,
            Category = request.Category ?? "Custom",
            Description = request.Description,
            ReportType = "Custom",
            DefaultColumns = JsonSerializer.Serialize(request.DefaultColumns),
            AvailableColumns = JsonSerializer.Serialize(request.AvailableColumns),
            AvailableFilters = JsonSerializer.Serialize(request.AvailableFilters),
            DefaultSort = request.DefaultSort,
            SqlTemplate = request.SqlTemplate,
            CountSqlTemplate = request.CountSqlTemplate,
            FilterSqlMapping = JsonSerializer.Serialize(request.FilterSqlMappings),
            IsEnabled = true,
            CreatedTime = DateTime.Now,
            ModifiedTime = DateTime.Now,
            CreatedBy = userName,
            ModifiedBy = userName,
        };

        _db.ReportConfigs.Add(entity);
        await _db.SaveChangesAsync();

        return ToReportConfigDto(entity);
    }

    public async Task<ReportConfigDto?> UpdateCustomReportAsync(int id, UpdateCustomReportRequest request)
    {
        var entity = await _db.ReportConfigs.FindAsync(id);
        if (entity == null || entity.ReportType != "Custom") return null;

        if (request.ReportName != null) entity.ReportName = request.ReportName;
        if (request.Category != null) entity.Category = request.Category;
        if (request.Description != null) entity.Description = request.Description;
        if (request.AvailableColumns != null) entity.AvailableColumns = JsonSerializer.Serialize(request.AvailableColumns);
        if (request.AvailableFilters != null) entity.AvailableFilters = JsonSerializer.Serialize(request.AvailableFilters);
        if (request.DefaultColumns != null) entity.DefaultColumns = JsonSerializer.Serialize(request.DefaultColumns);
        if (request.DefaultSort != null) entity.DefaultSort = request.DefaultSort;
        if (request.SqlTemplate != null) entity.SqlTemplate = request.SqlTemplate;
        if (request.CountSqlTemplate != null) entity.CountSqlTemplate = request.CountSqlTemplate;
        if (request.FilterSqlMappings != null) entity.FilterSqlMapping = JsonSerializer.Serialize(request.FilterSqlMappings);
        if (request.IsEnabled.HasValue) entity.IsEnabled = request.IsEnabled.Value;

        entity.ModifiedTime = DateTime.Now;
        await _db.SaveChangesAsync();

        return ToReportConfigDto(entity);
    }

    public async Task DeleteCustomReportAsync(int id)
    {
        var entity = await _db.ReportConfigs.FindAsync(id);
        if (entity != null && entity.ReportType == "Custom")
        {
            _db.ReportConfigs.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<CustomReportDetailDto?> GetCustomReportDetailAsync(int id)
    {
        var entity = await _db.ReportConfigs.FindAsync(id);
        if (entity == null || entity.ReportType != "Custom") return null;

        var filterMappings = !string.IsNullOrEmpty(entity.FilterSqlMapping)
            ? JsonSerializer.Deserialize<List<FilterSqlMappingItem>>(entity.FilterSqlMapping)
            : null;

        return new CustomReportDetailDto(
            entity.Id,
            entity.ReportCode ?? "",
            entity.ReportName ?? "",
            entity.Category,
            entity.Description,
            entity.ReportType,
            JsonSerializer.Deserialize<List<string>>(entity.DefaultColumns ?? "[]") ?? [],
            JsonSerializer.Deserialize<List<ReportColumnDefinition>>(entity.AvailableColumns ?? "[]") ?? [],
            JsonSerializer.Deserialize<List<ReportFilterDefinition>>(entity.AvailableFilters ?? "[]") ?? [],
            entity.DefaultSort,
            entity.IsEnabled,
            entity.SqlTemplate,
            entity.CountSqlTemplate,
            filterMappings
        );
    }

    public async Task<ReportQueryResult> ValidateCustomReportAsync(int id, Dictionary<string, object?>? filters = null)
    {
        filters ??= new Dictionary<string, object?>();
        return await QueryCustomReportPreviewAsync(id, filters, 10);
    }

    public async Task<ReportQueryResult> PreviewCustomReportAsync(int id, Dictionary<string, object?> filters)
    {
        return await QueryCustomReportPreviewAsync(id, filters, 10);
    }

    private async Task<ReportQueryResult> QueryCustomReportPreviewAsync(int id, Dictionary<string, object?> filters, int limit)
    {
        var config = await _db.ReportConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"报表配置 Id={id} 不存在");

        if (config.ReportType != "Custom")
            throw new InvalidOperationException("仅自定义报表支持验证和预览");

        var dataSql = _dynamicSqlProvider.BuildDataSql(config, filters, [], out var dataParams);

        // 设置查询超时
        var timeoutSeconds = int.Parse(_config["ReportSettings:CustomQueryTimeoutSeconds"] ?? "30");

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = AppendSqlServerPaging(dataSql);
        cmd.CommandTimeout = timeoutSeconds;

        foreach (var param in dataParams)
        {
            var dbParam = cmd.CreateParameter();
            dbParam.ParameterName = param.Key;
            dbParam.Value = param.Value ?? DBNull.Value;
            cmd.Parameters.Add(dbParam);
        }

        dataParams["@skip"] = 0;
        dataParams["@take"] = limit;

        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(dict);
        }

        var columns = rows.Count > 0 ? rows[0].Keys.ToList() : new List<string>();
        return new ReportQueryResult(rows.Count, 1, limit, columns, rows);
    }

    #endregion

    #region 私有方法

    private static ReportConfigDto ToReportConfigDto(ReportConfig config)
    {
        return new ReportConfigDto(
            config.Id,
            config.ReportCode ?? "",
            config.ReportName ?? "",
            config.Category,
            config.Description,
            config.ReportType,
            JsonSerializer.Deserialize<List<string>>(config.DefaultColumns ?? "[]") ?? [],
            JsonSerializer.Deserialize<List<ReportColumnDefinition>>(config.AvailableColumns ?? "[]") ?? [],
            JsonSerializer.Deserialize<List<ReportFilterDefinition>>(config.AvailableFilters ?? "[]") ?? [],
            config.DefaultSort,
            config.IsEnabled
        );
    }

    private static ReportExportTaskDto ToExportTaskDto(ReportExportTask task)
    {
        return new ReportExportTaskDto(
            task.TaskId ?? "",
            task.ReportCode ?? "",
            task.Status ?? "Unknown",
            task.FileName,
            task.FileSize,
            task.TotalRows,
            task.CompletedAt,
            task.ErrorMessage
        );
    }

    private static string NormalizeSort(string? sortField, string? sortDirection, string? defaultSort)
    {
        if (!string.IsNullOrEmpty(sortField))
        {
            // Q402 防御：白名单校验，防止 ORDER BY SQL 注入
            // 仅允许 [A-Za-z_][A-Za-z0-9_]* 或 schema.table.column 链式
            // 非法输入直接抛 ArgumentException，让 Controller 返回 400 给客户端
            if (!SqlSafety.IsValidOrderByColumn(sortField))
            {
                throw new ArgumentException($"排序字段包含非法字符，仅允许字母/数字/下划线的列名: {sortField}");
            }

            var dir = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            return $"{sortField} {dir}";
        }
        return defaultSort ?? "";
    }

    private static string AppendOrderBy(string sql, string sort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return sql;

        var upperSql = sql.ToUpperInvariant();
        if (upperSql.Contains("ORDER BY")) return sql;

        return $"{sql} ORDER BY {sort}";
    }

    private static string AppendSqlServerPaging(string sql)
    {
        // 如果已有 OFFSET 则不再添加
        if (sql.ToUpperInvariant().Contains("OFFSET")) return sql;

        return $"{sql} OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";
    }

    private static List<string> ExtractColumnNames(List<dynamic> rows)
    {
        if (rows.Count == 0) return [];

        var first = (IDictionary<string, object>)rows[0];
        return first.Keys.ToList();
    }

    #endregion
}
