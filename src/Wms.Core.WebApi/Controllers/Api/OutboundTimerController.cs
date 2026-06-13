using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Domain.Services;
using Wms.Core.Domain.Utilities.Response;

namespace Wms.Core.WebApi.Controllers.Api;

/// <summary>
/// 出库定时器 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
public partial class OutboundTimerController : ControllerBase
{
    private readonly IOutboundTimerService _outboundTimerService;
    private readonly ILogger<OutboundTimerController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public OutboundTimerController(
        IOutboundTimerService outboundTimerService,
        ILogger<OutboundTimerController> logger)
    {
        _outboundTimerService = outboundTimerService ?? throw new ArgumentNullException(nameof(outboundTimerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 1. 高温浸润出库
    /// </summary>
    [HttpPost("OutboundTaskByGaowen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> OutboundTaskByGaowen()
    {
        return await _outboundTimerService.ExecuteGaowenOutboundAsync();
    }
}
