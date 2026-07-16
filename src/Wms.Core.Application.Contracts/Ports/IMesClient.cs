using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Utilities.Response;

namespace Wms.Core.Application.Ports;

/// <summary>
/// MES 通信客户端接口
/// </summary>
public interface IMesClient
{
    /// <summary>
    /// 出入库保存 UploadMesInfo
    /// </summary>
    Task<MesResult> SaveUploadMesInfoAsync(string[] containerCodes, Location _loc, DateTime _currentTime, int opType);

    /// <summary>
    /// 自动分档保存 UploadMesInfo
    /// </summary>
    /// <param name="_ui"></param>
    /// <param name="_loc"></param>
    /// <returns></returns>
    Task<MesResult> SaveUploadMesInfoByAutomaticAsync(UnitloadItem _ui, Location _loc);

    /// <summary>
    /// 手工分档保存 UploadMesInfo
    /// </summary>
    /// <param name="batteryCode"></param>
    /// <param name="Level"></param>
    /// <param name="_loc"></param>
    /// <returns></returns>
    Task<MesResult> SaveUploadMesInfoManualAsync(string batteryCode, string Level, Location _loc);

    /// <summary>
    /// 获取排废信息
    /// </summary>
    Task<MesResult> GetWasteDischargeInfoAsync(Unitload unitload);

    /// <summary>
    /// 推送 UploadMesInfo 的 MestextInfo 到 MES 批量接口（队列消费端调用）
    /// </summary>
    Task<MesResult> PushMesInfoAsync(string mestextInfo);
}

/// <summary>
/// MES 客户端配置选项
/// </summary>
public class MesClientOptions
{
    /// <summary>
    /// 
    /// </summary>
    public const string SectionName = "Mes";

    /// <summary>
    /// 是否启用 MES 上传
    /// </summary>
    public bool Enable { get; set; } = false;

    /// <summary>
    /// MES 服务地址
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 请求超时时间（秒），默认 10 秒
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 重试次数，默认 3 次
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 熔断器失败阈值，默认 5 次
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// 熔断器恢复时间（秒），默认 30 秒
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
