using Wms.Core.Domain.Utilities.Response;

namespace Wms.Core.Domain.Services;

/// <summary>
/// 出库定时器服务接口
/// </summary>
public interface IOutboundTimerService
{
    /// <summary>
    /// 执行高温浸润出库任务
    /// </summary>
    Task<WcsResult> ExecuteGaowenOutboundAsync();
}
