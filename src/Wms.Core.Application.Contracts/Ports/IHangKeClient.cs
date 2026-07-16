using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Utilities.Response;

namespace Wms.Core.Application.Ports;

/// <summary>
/// 杭可设备通信客户端接口
/// </summary>
public interface IHangKeClient
{
    /// <summary>
    /// 注销托盘
    /// </summary>
    Task<ResultInfo> CancelTrayAsync(string TrayCode);

    /// <summary>
    /// 化成组盘
    /// </summary>
    Task<ResultInfo> ChemicalPalletizeAsync(Unitload unitload);

    /// <summary>
    /// 分容组盘
    /// </summary>
    Task<ResultInfo> SeparatePalletizeAsync(Unitload unitload);

    /// <summary>
    /// 获取排废信息
    /// </summary>
    Task<ResultInfo> GetDischargeInfoAsync(Unitload unitload);

    /// <summary>
    /// 出入口通知
    /// </summary>
    Task<ResultInfo> InOutNotifyAsync(string Position, string TrayCode, InOutType_Enum InOutType);

    /// <summary>
    /// 获取电芯数据
    /// </summary>
    Task<ResultInfo> GetCellDataAsync(string CellSn);
}

/// <summary>
/// 杭可客户端配置选项
/// </summary>
public class HangKeClientOptions
{
    /// <summary>
    /// 
    /// </summary>
    public const string SectionName = "HangKe";

    /// <summary>
    /// 是否启用杭可通知
    /// </summary>
    public bool Enable { get; set; } = false;

    /// <summary>
    /// 杭可设备通信用户名（C4：原为 const 硬编码 "TSGX2ZZ"，已改为可通过配置注入）
    /// </summary>
    /// <remarks>
    /// 默认值见 <c>appsettings.json</c> 的 <c>HangKe:UserName</c> 节（杭可设备出厂凭据）。
    /// 配置来源优先级：环境变量 <c>HangKe__UserName</c> &gt; appsettings.json <c>HangKe:UserName</c>。
    /// 生产环境如需更换凭据，推荐通过环境变量覆盖，无需改动代码或 appsettings.json。
    /// </remarks>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 杭可设备通信密码（C4：原为 const 硬编码 "ZZ@123"，已改为可通过配置注入）
    /// </summary>
    /// <remarks>
    /// 默认值见 <c>appsettings.json</c> 的 <c>HangKe:PassWord</c> 节（杭可设备出厂凭据）。
    /// 配置来源优先级：环境变量 <c>HangKe__PassWord</c> &gt; appsettings.json <c>HangKe:PassWord</c>。
    /// 生产环境如需更换凭据，推荐通过环境变量覆盖，无需改动代码或 appsettings.json。
    /// </remarks>
    public string PassWord { get; set; } = string.Empty;

    /// <summary>
    /// 杭可服务地址
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
