using Wms.Core.Domain.Entities.Transport;

namespace Wms.Core.Domain.Interfaces;

/// <summary>
/// ctask 数据库访问接口（Dapper 实现）
/// </summary>
public interface ICtaskDbService
{
    /// <summary>
    /// 写入搬运任务到 wcs_tasks
    /// </summary>
    Task WriteTaskAsync(WcsTask task);

    /// <summary>
    /// 根据 task_code 查询任务
    /// </summary>
    Task<WcsTask?> ReadByTaskCodeAsync(string taskCode);

    /// <summary>
    /// 查询所有未完成的任务（wcs_state 不等于 已完成/失败/已取消）
    /// </summary>
    Task<IReadOnlyList<WcsTask>> ReadPendingTasksAsync();

    /// <summary>
    /// 查询 updated_at 大于指定时间的任务（增量同步）
    /// </summary>
    Task<IReadOnlyList<WcsTask>> ReadTasksUpdatedAfterAsync(DateTime since);

    /// <summary>
    /// 更新 WMS 端状态
    /// </summary>
    Task UpdateWmsStateAsync(string taskCode, string state);

    /// <summary>
    /// 更新 WCS 端状态（强制完成/取消）
    /// </summary>
    Task UpdateWcsStateAsync(string taskCode, string wcsState, DateTime? completedAt = null);
}
