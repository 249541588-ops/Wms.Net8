using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Enums;
using Wms.Core.Application.Ports;

namespace Wms.Core.Infrastructure.Persistence;

/// <summary>
/// ctask 数据库访问服务（Dapper 实现）
/// </summary>
/// <remarks>
/// 使用独立 SqlConnection 访问 ctask 数据库（与 WmsDb 不同数据库，不能复用 EF Core 连接）。
/// 连接字符串从 IConfiguration 中获取，Key 为 "ConnectionStrings:CtaskConnection"。
/// </remarks>
public class CtaskDbService : ICtaskDbService
{
    private readonly string _connectionString;
    private readonly ILogger<CtaskDbService> _logger;

    /// <summary>
    /// 初始化 ctask 数据库服务
    /// </summary>
    static CtaskDbService()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public CtaskDbService(IConfiguration configuration, ILogger<CtaskDbService> logger)
    {
        _connectionString = configuration.GetConnectionString("CtaskConnection")
            ?? throw new InvalidOperationException("CtaskConnection 连接字符串未配置");
        _logger = logger;
    }

    /// <summary>
    /// 写入搬运任务到 wcs_tasks
    /// </summary>
    public async Task WriteTaskAsync(WcsTask task)
    {
        const string sql = @"
            INSERT INTO wcs_tasks (
                task_code, task_type, cont_code, cont_type, start_loc, end_loc,
                act_end_loc, prio, sent_at, wms_state, wms_failuir_times, wcs_state,
                completed_at, location_group, updated_at, err_code, err_msg,
                wms_note, wcs_note, ex1, ex2, warehouse
            ) VALUES (
                @TaskCode, @TaskType, @ContCode, @ContType, @StartLoc, @EndLoc,
                @ActEndLoc, @Prio, @SentAt, @WmsState, @WmsFailuirTimes, @WcsState,
                @CompletedAt, @LocationGroup, @UpdatedAt, @ErrCode, @ErrMsg,
                @WmsNote, @WcsNote, @Ex1, @Ex2, @Warehouse
            )";

        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, task);
        _logger.LogDebug("[CtaskDb] 写入 wcs_tasks: {TaskCode}", task.TaskCode);
    }

    /// <summary>
    /// 根据 task_code 查询任务
    /// </summary>
    public async Task<WcsTask?> ReadByTaskCodeAsync(string taskCode)
    {
        const string sql = "SELECT * FROM wcs_tasks WHERE task_code = @TaskCode";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<WcsTask>(sql, new { TaskCode = taskCode });
    }

    /// <summary>
    /// 查询所有未完成的任务
    /// </summary>
    public async Task<IReadOnlyList<WcsTask>> ReadPendingTasksAsync()
    {
        const string sql = @"
            SELECT * FROM wcs_tasks
            WHERE wcs_state NOT IN (@Completed, @Failed, @Cancelled)
            ORDER BY prio DESC, sent_at ASC";

        using var conn = new SqlConnection(_connectionString);
        var results = await conn.QueryAsync<WcsTask>(sql, new
        {
            Completed = TaskInfoWcsStates.Completed,
            Failed = TaskInfoWcsStates.Refused,
            Cancelled = TaskInfoWcsStates.Cancelled
        });
        return results.ToList();
    }

    /// <summary>
    /// 查询 updated_at 大于指定时间的任务（增量同步）
    /// </summary>
    public async Task<IReadOnlyList<WcsTask>> ReadTasksUpdatedAfterAsync(DateTime since)
    {
        const string sql = @"
            SELECT * FROM wcs_tasks
            WHERE updated_at > @Since
            ORDER BY updated_at ASC";

        using var conn = new SqlConnection(_connectionString);
        var results = await conn.QueryAsync<WcsTask>(sql, new { Since = since });
        return results.ToList();
    }

    /// <summary>
    /// 更新 WMS 端状态
    /// </summary>
    public async Task UpdateWmsStateAsync(string taskCode, string state)
    {
        const string sql = @"
            UPDATE wcs_tasks
            SET wms_state = @State, updated_at = GETDATE()
            WHERE task_code = @TaskCode";

        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { TaskCode = taskCode, State = state });
        _logger.LogDebug("[CtaskDb] 更新 wms_state: {TaskCode} → {State}", taskCode, state);
    }

    /// <summary>
    /// 更新 WCS 端状态（强制完成/取消）
    /// </summary>
    public async Task UpdateWcsStateAsync(string taskCode, string wcsState, DateTime? completedAt = null)
    {
        const string sql = @"
            UPDATE wcs_tasks
            SET wcs_state = @WcsState, completed_at = @CompletedAt, updated_at = GETDATE()
            WHERE task_code = @TaskCode";

        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { TaskCode = taskCode, WcsState = wcsState, CompletedAt = completedAt });
        _logger.LogInformation("[CtaskDb] 更新 wcs_state: {TaskCode} → {WcsState}, completed_at={CompletedAt}", taskCode, wcsState, completedAt);
    }

    /// <summary>
    /// 清理已完成处理的 WCS 任务（分批删除，仅 wms_state='archived'）
    /// </summary>
    public async Task<int> CleanupArchivedTasksAsync(int retentionDays)
    {
        var threshold = DateTime.UtcNow.AddDays(-retentionDays);
        int total = 0, deleted;
        const string sql = @"
            DELETE TOP (5000) FROM wcs_tasks
            WHERE wms_state = @State AND updated_at < @Threshold";

        using var conn = new SqlConnection(_connectionString);
        do
        {
            deleted = await conn.ExecuteAsync(sql, new { State = "archived", Threshold = threshold });
            total += deleted;
            if (deleted > 0) await Task.Delay(50);
        } while (deleted > 0);

        _logger.LogInformation("[CtaskDb] wcs_tasks 清理完成，删除 {Count} 条", total);
        return total;
    }

    /// <summary>
    /// 根据 task_code 删除任务（用于回滚）
    /// </summary>
    public async Task<int> DeleteTaskAsync(string taskCode)
    {
        const string sql = "DELETE FROM wcs_tasks WHERE task_code = @TaskCode";

        using var conn = new SqlConnection(_connectionString);
        var affected = await conn.ExecuteAsync(sql, new { TaskCode = taskCode });
        _logger.LogDebug("[CtaskDb] 删除 wcs_tasks: TaskCode={TaskCode}, Affected={Affected}", taskCode, affected);
        return affected;
    }
}
