using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Application.Ports;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 数据定时清理服务（Hangfire 调用）
/// </summary>
public class DataCleanupService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataCleanupService> _logger;
    private const int BatchSize = 5000;
    private const int CommandTimeoutSeconds = 120;

    public DataCleanupService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DataCleanupService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行全部数据清理（7 个事务组，跨 3 个数据库）
    /// </summary>
    public async Task CleanupAllAsync()
    {
        var result = new Dictionary<string, int>();

        // ========== 组1~5: WmsDb ==========
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
            db.Database.SetCommandTimeout(CommandTimeoutSeconds);

            // 组1: 归档父子表（先子后父，同一事务保证原子性）
            await CleanupGroupAsync(db, "归档数据", result, async () =>
            {
                var t = DateTime.UtcNow.AddDays(-GetRetentionDays("ArchivedTasks", 90));
                await BatchDeleteAsync(db, "ArchivedUnitloadItemDetails", result,
                    "UnitloadItemId IN (SELECT Id FROM ArchivedUnitloadItems WHERE UnitloadId IN (SELECT Id FROM ArchivedUnitloads WHERE ArchivedAt < @t AND ArchivedAt IS NOT NULL))", t);
                await BatchDeleteAsync(db, "ArchivedUnitloadItems", result,
                    "UnitloadId IN (SELECT Id FROM ArchivedUnitloads WHERE ArchivedAt < @t AND ArchivedAt IS NOT NULL)", t);
                await BatchDeleteAsync(db, "ArchivedUnitloads", result, "ArchivedAt < @t AND ArchivedAt IS NOT NULL", t);
                await BatchDeleteAsync(db, "ArchivedTasks", result, "ArchivedAt < @t AND ArchivedAt IS NOT NULL", t);
            });

            // 组2: 操作日志
            await CleanupGroupAsync(db, "操作日志", result, async () =>
            {
                await BatchDeleteAsync(db, "SystemLogs", result, "OperationTime < @t AND OperationTime IS NOT NULL",
                    DateTime.UtcNow.AddDays(-GetRetentionDays("SystemLogs", 30)));
                await BatchDeleteAsync(db, "UnitloadOps", result, "CreatedTime < @t AND CreatedTime IS NOT NULL",
                    DateTime.UtcNow.AddDays(-GetRetentionDays("UnitloadOps", 60)));
                await BatchDeleteAsync(db, "LocationOps", result, "CreatedTime < @t AND CreatedTime IS NOT NULL",
                    DateTime.UtcNow.AddDays(-GetRetentionDays("LocationOps", 60)));
                await BatchDeleteAsync(db, "BatteryOps", result, "CreateAt < @t AND CreateAt IS NOT NULL",
                    DateTime.UtcNow.AddDays(-GetRetentionDays("BatteryOps", 90)));
            });

            // 组3: 业务数据
            await CleanupGroupAsync(db, "业务数据", result, async () =>
            {
                await BatchDeleteAsync(db, "UploadMesInfo", result, "ctime < @t AND ctime IS NOT NULL",
                    DateTime.UtcNow.AddDays(-GetRetentionDays("UploadMesInfo", 60)));
                await BatchDeleteAsync(db, "Flows", result, "CreatedTime < @t AND CreatedTime IS NOT NULL",
                    DateTime.UtcNow.AddDays(-GetRetentionDays("Flows", 90)));
                await BatchDeleteAsync(db, "BatteryCells", result, "CreatedTime < @t AND CreatedTime IS NOT NULL",
                    DateTime.UtcNow.AddDays(-GetRetentionDays("BatteryCells", 90)));
            });

            // 组4: 流程引擎（FlowNodeLog 有 FK 到 FlowInstance，先子后父）
            await CleanupGroupAsync(db, "流程引擎", result, async () =>
            {
                var flowT = DateTime.UtcNow.AddDays(-GetRetentionDays("FlowInstances", 60));
                await BatchDeleteAsync(db, "FlowNodeLogs", result, "CreatedTime < @t AND CreatedTime IS NOT NULL",
                    DateTime.UtcNow.AddDays(-GetRetentionDays("FlowNodeLogs", 60)));
                await BatchDeleteAsync(db, "FlowInstances", result, "CreatedTime < @t AND CreatedTime IS NOT NULL", flowT);
            });

            // 组5: 认证
            await CleanupGroupAsync(db, "认证数据", result, async () =>
            {
                await BatchDeleteAsync(db, "RefreshTokens", result, "ExpiryTime < @t AND ExpiryTime IS NOT NULL",
                    DateTime.UtcNow.AddDays(-GetRetentionDays("RefreshTokens", 30)));
            });
        }

        // ========== 组6: WmsLogsDb ==========
        try
        {
            using var logScope = _scopeFactory.CreateScope();
            var logDb = logScope.ServiceProvider.GetService<WmsLogDbContext>();
            if (logDb != null)
            {
                logDb.Database.SetCommandTimeout(CommandTimeoutSeconds);
                await CleanupGroupAsync(logDb, "接口日志", result, async () =>
                {
                    await BatchDeleteAsync(logDb, "InterfaceLogs", result, "CreatedTime < @t AND CreatedTime IS NOT NULL",
                        DateTime.UtcNow.AddDays(-GetRetentionDays("InterfaceLogs", 30)));
                });
            }
            else
            {
                _logger.LogWarning("[DataCleanup] WmsLogDbContext 未注册，跳过接口日志清理");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DataCleanup] 接口日志清理失败: {Msg}", ex.Message);
        }

        // ========== 组7: ctask ==========
        try
        {
            using var ctaskScope = _scopeFactory.CreateScope();
            var ctaskDb = ctaskScope.ServiceProvider.GetRequiredService<ICtaskDbService>();
            result["WcsTasks"] = await ctaskDb.CleanupArchivedTasksAsync(GetRetentionDays("WcsTasks", 30));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DataCleanup] WCS任务清理失败: {Msg}", ex.Message);
        }

        _logger.LogInformation("[DataCleanup] 清理完成: {Summary}",
            string.Join(", ", result.Select(kv => $"{kv.Key}:{kv.Value}")));
    }

    private int GetRetentionDays(string key, int defaultValue)
        => _configuration.GetValue<int>($"DataCleanup:RetentionDays:{key}", defaultValue);

    private async Task BatchDeleteAsync(DbContext db, string table, Dictionary<string, int> result,
        string whereClause, DateTime threshold)
    {
        int total = 0, deleted;
        do
        {
            deleted = await db.Database.ExecuteSqlRawAsync(
                $"DELETE TOP ({BatchSize}) FROM [{table}] WHERE {whereClause}",
                new SqlParameter("@t", threshold));
            total += deleted;
            if (deleted > 0) await Task.Delay(50);
        } while (deleted > 0);

        if (total > 0)
        {
            result[table] = total;
            _logger.LogInformation("[DataCleanup] {Table}: 删除 {Count} 条", table, total);
        }
    }

    private async Task CleanupGroupAsync(DbContext db, string groupName, Dictionary<string, int> result,
        Func<Task> action)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await action();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DataCleanup] {Group}清理失败: {Msg}", groupName, ex.Message);
        }
    }
}
