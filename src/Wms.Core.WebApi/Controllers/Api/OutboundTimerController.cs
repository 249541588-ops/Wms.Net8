using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.WebApi.Filters;

namespace Wms.Core.WebApi.Controllers.Api;

/// <summary>
/// 出库定时器 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
[InternalIpWhitelist]
public partial class OutboundTimerController : ControllerBase
{
    private readonly IOutboundTimerService _outboundTimerService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public OutboundTimerController(
        IOutboundTimerService outboundTimerService)
    {
        _outboundTimerService = outboundTimerService ?? throw new ArgumentNullException(nameof(outboundTimerService));
    }

    /// <summary>
    /// 1. 上传给 Mes（推送 UploadMesInfo 队列）
    /// </summary>
    [HttpPost("WmsSendToMes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> WmsSendToMes()
    {
        return await _outboundTimerService.SendPendingMesAsync();
    }

    /// <summary>
    /// 2. 杭可申请取盘
    /// </summary>
    [HttpPost("OutboundTaskPickPallet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskPickPallet()
    {
        return await _outboundTimerService.PickPendingPalletAsync();
    }

    /// <summary>
    /// 3. 杭可申请移库
    /// </summary>
    [HttpPost("OutboundTaskMove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskMove()
    {
        return await _outboundTimerService.MovePendingAsync();
    }

    /// <summary>
    /// 4. 化成出库
    /// </summary>
    [HttpPost("OutboundTaskByHc")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskByHc()
    {
        return await _outboundTimerService.ExecuteHcOutboundAsync();
    }

    /// <summary>
    /// 5. 分容出库
    /// </summary>
    [HttpPost("OutboundTaskByFr")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskByFr()
    {
        return await _outboundTimerService.ExecuteFrOutboundAsync();
    }

    /// <summary>
    /// 6. 空托盘出库
    /// </summary>
    /// <param name="exitCode"></param>
    /// <returns></returns>
    [HttpPost("OutboundTaskByEmpty")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskByEmpty(string exitCode)
    {
        return await _outboundTimerService.ExecuteEmptyOutboundAsync(exitCode);
    }

    /// <summary>
    /// 7. 高温浸润出库
    /// </summary>
    [HttpPost("OutboundTaskByGaowen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskByGaowen()
    {
        return await _outboundTimerService.ExecuteGaowenOutboundAsync();
    }

    /// <summary>
    /// 7-1. 高温浸润出库（双叉）
    /// </summary>
    [HttpPost("OutboundTaskByGaowenDouble")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskByGaowenDouble()
    {
        return await _outboundTimerService.ExecuteGaowenDoubleOutboundAsync();
    }

    /// <summary>
    /// 8. 一库（常温一天）出库
    /// </summary>
    [HttpPost("OutboundTaskByOneday")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskByOneday()
    {
        return await _outboundTimerService.ExecuteOnedayOutboundAsync();
    }

    /// <summary>
    /// 9. 七天库（常温七天）出库
    /// </summary>
    [HttpPost("OutboundTaskBySevenDay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskBySevenDay()
    {
        return await _outboundTimerService.ExecuteSevenDayOutboundAsync();
    }

    /// <summary>
    /// 10. 成品库出库
    /// </summary>
    [HttpPost("OutboundTaskByFinishedProduct")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskByFinishedProduct()
    {
        return await _outboundTimerService.ExecuteFinishedProductOutboundAsync();
    }
}
