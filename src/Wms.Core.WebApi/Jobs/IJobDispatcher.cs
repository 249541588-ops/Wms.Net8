namespace Wms.Core.WebApi.Jobs;

/// <summary>
/// 定时任务调度器接口
/// </summary>
public interface IJobDispatcher
{
    /// <summary>
    /// 执行任务（从 DB 读取配置，支持 internal 和 http-call 两种模式）
    /// </summary>
    Task ExecuteAsync(Guid jobId);

    /// <summary>
    /// 获取已注册的内部方法列表（供前端下拉选择）
    /// </summary>
    IReadOnlyList<InternalMethodInfo> GetInternalMethods();
}

/// <summary>
/// 内部方法信息
/// </summary>
public record InternalMethodInfo(string MethodId, string DisplayName, string Description, string DefaultCron);
