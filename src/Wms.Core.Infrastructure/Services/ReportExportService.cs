using System.Text.Json;
using ClosedXML.Excel;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Services;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Services.ReportProviders;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 报表导出服务实现 — Hangfire 后台执行，Dapper 分批读取，ClosedXML 流式写入
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnumerable<IReportQueryProvider> _providers;
    private readonly IConfiguration _config;
    private readonly ILogger<ReportExportService> _logger;
    private readonly string _connectionString;

    public ReportExportService(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IReportQueryProvider> providers,
        IConfiguration config,
        ILogger<ReportExportService> logger)
    {
        _scopeFactory = scopeFactory;
        _providers = providers;
        _config = config;
        _logger = logger;
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection 连接字符串未配置");
    }

    public async Task ExecuteExportAsync(string taskId, string reportCode, Dictionary<string, object?> filters, List<string>? columns, string filePath)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var dynamicSqlProvider = scope.ServiceProvider.GetRequiredService<DynamicSqlProvider>();

        int totalRows = 0;

        try
        {
            // 更新任务状态为 Processing
            var task = await db.ReportExportTasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
            if (task == null)
            {
                _logger.LogError("导出任务不存在: TaskId={TaskId}", taskId);
                return;
            }

            task.Status = "Processing";
            task.StartedAt = DateTime.Now;
            await db.SaveChangesAsync();

            // 获取报表配置
            var config = await db.ReportConfigs
                .FirstOrDefaultAsync(r => r.ReportCode == reportCode && r.IsEnabled)
                ?? throw new InvalidOperationException($"报表 {reportCode} 不存在或未启用");

            // 构建 field → title 映射，用于 Excel 中文列头
            var colTitleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(config.AvailableColumns))
            {
                var colDefs = JsonSerializer.Deserialize<List<ReportColumnDefinition>>(config.AvailableColumns);
                if (colDefs != null)
                    foreach (var c in colDefs)
                        colTitleMap[c.Field] = c.Title;
            }

            // 构建 SQL（不带分页）
            string dataSql;
            Dictionary<string, object> sqlParams;

            if (config.ReportType == "Custom")
            {
                var effectiveColumns = columns ?? JsonSerializer.Deserialize<List<string>>(config.DefaultColumns ?? "[]") ?? [];
                dataSql = dynamicSqlProvider.BuildDataSql(config, filters, effectiveColumns, out sqlParams);
            }
            else
            {
                var provider = _providers.FirstOrDefault(p => p.ReportCode == reportCode)
                    ?? throw new InvalidOperationException($"报表 {reportCode} 的查询 Provider 未注册");

                var effectiveColumns = columns ?? JsonSerializer.Deserialize<List<string>>(config.DefaultColumns ?? "[]") ?? [];
                dataSql = provider.BuildDataSql(filters, effectiveColumns, out sqlParams);
            }

            var batchSize = int.Parse(_config["ReportSettings:ExportBatchSize"] ?? "5000");
            var exportDir = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(exportDir);

            // 使用 ClosedXML 创建 Excel 文件
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("数据");

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int offset = 0;
            bool hasHeader = false;

            while (true)
            {
                sqlParams["@skip"] = offset;
                sqlParams["@take"] = batchSize;

                var pagedSql = $"{dataSql} ORDER BY (SELECT NULL) OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";
                var rows = (await conn.QueryAsync<dynamic>(pagedSql, sqlParams)).ToList();

                if (rows.Count == 0) break;

                if (!hasHeader && rows.Count > 0)
                {
                    var first = (IDictionary<string, object>)rows[0];
                    var colNames = first.Keys.ToList();
                    for (int i = 0; i < colNames.Count; i++)
                    {
                        var header = colTitleMap.TryGetValue(colNames[i], out var title) ? title : colNames[i];
                        worksheet.Cell(1, i + 1).Value = header;
                    }
                    hasHeader = true;
                }

                var startRow = offset + 2; // 第1行是表头
                for (int i = 0; i < rows.Count; i++)
                {
                    var dict = (IDictionary<string, object>)rows[i];
                    var colNames = dict.Keys.ToList();
                    for (int j = 0; j < colNames.Count; j++)
                    {
                        var val = dict[colNames[j]];
                        var cell = worksheet.Cell(startRow + i, j + 1);
                        if (val == null || val == DBNull.Value)
                        {
                            cell.SetValue(string.Empty);
                        }
                        else if (val is decimal d)
                        {
                            cell.SetValue((double)d);
                        }
                        else if (val is int or long or short or byte or uint or ulong or ushort or sbyte or float or double)
                        {
                            cell.SetValue(Convert.ToDouble(val));
                        }
                        else if (val is DateTime dt)
                        {
                            cell.SetValue(dt);
                            cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
                        }
                        else if (val is bool b)
                        {
                            cell.SetValue(b);
                        }
                        else
                        {
                            cell.SetValue(val.ToString());
                        }
                    }
                }

                totalRows += rows.Count;
                offset += rows.Count;

                // 释放内存
                rows.Clear();
                if (offset % (batchSize * 10) == 0)
                    GC.Collect();
            }

            // 保存文件
            workbook.SaveAs(filePath);

            // 更新任务状态为 Completed
            var fileInfo = new FileInfo(filePath);
            task.Status = "Completed";
            task.FileSize = fileInfo.Length;
            task.TotalRows = totalRows;
            task.CompletedAt = DateTime.Now;
            await db.SaveChangesAsync();

            _logger.LogInformation("报表导出完成: TaskId={TaskId}, Rows={TotalRows}, FileSize={FileSize}bytes", taskId, totalRows, fileInfo.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "报表导出失败: TaskId={TaskId}", taskId);

            // 更新任务状态为 Failed
            try
            {
                using var errorScope = _scopeFactory.CreateScope();
                var errorDb = errorScope.ServiceProvider.GetRequiredService<WmsDbContext>();
                var errorTask = await errorDb.ReportExportTasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
                if (errorTask != null)
                {
                    errorTask.Status = "Failed";
                    errorTask.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                    errorTask.TotalRows = totalRows;
                    errorTask.CompletedAt = DateTime.Now;
                    await errorDb.SaveChangesAsync();
                }
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "更新导出任务失败状态时出错");
            }
        }
    }

    public string GetFilePath(string taskId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();

        var task = db.ReportExportTasks.FirstOrDefault(t => t.TaskId == taskId);
        return task?.FilePath ?? "";
    }

    public async Task DeleteExportFileAsync(string taskId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();

        var task = await db.ReportExportTasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
        if (task != null)
        {
            // 删除文件
            if (!string.IsNullOrEmpty(task.FilePath) && File.Exists(task.FilePath))
            {
                File.Delete(task.FilePath);
            }

            db.ReportExportTasks.Remove(task);
            await db.SaveChangesAsync();
        }
    }
}
