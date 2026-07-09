using Wms.Core.Domain.Utilities.Response;

namespace Wms.Core.Application.Ports;

/// <summary>
/// 出库定时器服务接口
/// </summary>
public interface IOutboundTimerService
{
    /// <summary>
    /// 执行高温浸润出库任务 - 双叉模式
    /// </summary>
    Task<WcsResult> ExecuteGaowenDoubleOutboundAsync();

    /// <summary>
    /// 执行高温浸润出库任务
    /// </summary>
    Task<WcsResult> ExecuteGaowenOutboundAsync();

    /// <summary>
    /// 执行一天出库任务
    /// </summary>
    Task<WcsResult> ExecuteOnedayOutboundAsync();

    /// <summary>
    /// 执行七天出库任务
    /// </summary>
    Task<WcsResult> ExecuteSevenDayOutboundAsync();

     /// <summary>
    /// 执行成品出库任务
    /// </summary>
    Task<WcsResult> ExecuteFinishedProductOutboundAsync();

    /// <summary>
    /// 推送待上传的 MES 信息（UploadMesInfo 队列消费）
    /// </summary>
    Task<WcsResult> SendPendingMesAsync();

    /// <summary>
    /// 杭可申请取盘（扫描待取盘货位，请求杭可设备取盘）
    /// </summary>
    Task<WcsResult> PickPendingPalletAsync();

    /// <summary>
    /// 杭可申请移库（扫描 HKPosintionState=8 的货位，触发库内移动）
    /// </summary>
    Task<WcsResult> MovePendingAsync();

    /// <summary>
    /// 执行化成出库任务
    /// </summary>
    Task<WcsResult> ExecuteHcOutboundAsync();

    /// <summary>
    /// 执行分容出库任务
    /// </summary>
    Task<WcsResult> ExecuteFrOutboundAsync();

    /// <summary>
    /// 执行空托盘出库任务
    /// </summary>
    /// <param name="exitCode">数据字典编码</param>
    /// <returns></returns>
    Task<WcsResult> ExecuteEmptyOutboundAsync(string exitCode);
}
