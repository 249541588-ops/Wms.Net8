using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Utilities.Response;

namespace Wms.Core.Application.Ports;

/// <summary>
/// WCS 通信客户端接口（适配器模式）
/// </summary>
/// <remarks>
/// 后续可按设备厂商创建不同实现，通过 DI 切换
/// </remarks>
public interface IWcsClient
{
    /// <summary>
    /// 下发搬运任务到 WCS
    /// </summary>
    /// <param name="task">运输任务</param>
    /// <returns>WCS 响应结果</returns>
    Task<WcsResult> SendTaskAsync(TransTask task);

    /// <summary>
    /// 查询设备状态
    /// </summary>
    /// <param name="equipmentId">设备编号</param>
    /// <returns>WCS 响应结果（含设备状态数据）</returns>
    Task<WcsResult> GetEquipmentStatusAsync(string equipmentId);

    /// <summary>
    /// 上传 MES 信息到 WCS
    /// </summary>
    /// <param name="info">MES 上传信息</param>
    /// <returns>WCS 响应结果</returns>
    Task<WcsResult> UploadMesInfoAsync(UploadMesInfo info);
}

/// <summary>
/// WCS 客户端配置选项
/// </summary>
public class WcsClientOptions
{
    public const string SectionName = "Wcs";

    /// <summary>
    /// WCS 服务地址
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
