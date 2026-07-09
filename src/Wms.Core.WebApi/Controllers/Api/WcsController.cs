using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Wms.Core.Domain.Exceptions;
using Wms.Core.WebApi.Hubs;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Wms.Core.WebApi.Filters;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Repositories;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Helpers;
using Wms.Core.WebApi.Services.Wcs;
using WcsReqHandler = Wms.Core.Application.Handlers.WcsRequest.IWcsRequestHandler;

namespace Wms.Core.WebApi.Controllers.Api;

/// <summary>
/// Wcs API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
//[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
[InternalIpWhitelist]
public partial class WcsController : ControllerBase
{
    private readonly ILocationService _locationService;
    private readonly IUnitloadService _unitloadService;
    private readonly IRepository<Unitload, int> _repository;
    private readonly IEnumerable<WcsReqHandler> _requestHandlers;
    private readonly ILogger<WcsController> _logger;
    private readonly WcsTaskSyncService? _wcsTaskSyncService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFlowEngine _flowEngine;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IHubContext<WmsHub> _hub;
    private static readonly ConcurrentDictionary<string, DateTime> _recentRequests = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public WcsController(
        ILocationService locationService,
        IUnitloadService unitloadService,
        IRepository<Unitload, int> repository,
        IEnumerable<WcsReqHandler> requestHandlers,
        ILogger<WcsController> logger,
        IServiceScopeFactory scopeFactory,
        IFlowEngine flowEngine,
        IBackgroundTaskQueue taskQueue,
        IHubContext<WmsHub> hub,
        WcsTaskSyncService? wcsTaskSyncService = null)
    {
        _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
        _unitloadService = unitloadService ?? throw new ArgumentNullException(nameof(unitloadService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _requestHandlers = requestHandlers ?? throw new ArgumentNullException(nameof(requestHandlers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _flowEngine = flowEngine ?? throw new ArgumentNullException(nameof(flowEngine));
        _taskQueue = taskQueue ?? throw new ArgumentNullException(nameof(taskQueue));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _wcsTaskSyncService = wcsTaskSyncService;
    }

    /// <summary>
    /// 1. 自动装盘
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("AutoBindingPlate")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public WcsResult AutoBindingPlate([FromBody] WcsRequest request)
    {
        try
        {
            var result = _unitloadService.CreateUnitloadAutomatic(request);
            if (result.IsSuccess)
            {
                return ApiResultHelper.WcsSuccess($"成功", ResultCodeTypes.一, 1);
            }
            else
                return ApiResultHelper.WcsFail($"{result.Error}", ResultCodeTypes.程序异常, -1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            return ApiResultHelper.WcsFail("服务器内部错误", ResultCodeTypes.程序异常, -1);
        }
    }

    /// <summary>
    /// 2. WCS 任务请求（策略分发模式）
    /// </summary>
    [HttpPost("WcsRequest")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> WcsRequest([FromBody] WcsRequest requestInfo)
    {
        WcsResult _wcsResult = new WcsResult();
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        if (requestInfo == null)
        {
            throw new InvalidRequestException("请求数据不能空");
        }
        if (string.IsNullOrWhiteSpace(requestInfo.LocationCode))
        {
            throw new InvalidRequestException("请求位置不能空！");
        }

        string containerCode = "";
        if (requestInfo.ContainerCode != null && requestInfo.ContainerCode.Length > 0)
        {
            containerCode = string.Join(",", requestInfo.ContainerCode);
        }
        else
        {
            throw new InvalidRequestException("请求条码不能空！");
        }

        // WCS 接口日志记录
        var log = new InterfaceLog
        {
            Source = "WCS",
            Endpoint = "WcsRequest",
            Requester = HttpContext.Connection.RemoteIpAddress?.ToString(),
            LocationCode = requestInfo.LocationCode,
            ContainerCode = containerCode,
            RequestBody = Truncate(JsonConvert.SerializeObject(requestInfo), 8000),
            CreatedTime = DateTime.UtcNow,
        };

        // 重复请求检测（5 秒内相同请求）
        var fingerprint = $"WcsRequest:{requestInfo.LocationCode}:{containerCode}";
        var now = DateTime.UtcNow;
        if (_recentRequests.TryGetValue(fingerprint, out var lastTime)
            && (now - lastTime).TotalSeconds < 5)
        {
            log.IsDuplicate = true;
        }
        _recentRequests[fingerprint] = now;

        // 定期清理过期指纹
        if (_recentRequests.Count > 1000)
        {
            var expired = _recentRequests.Where(kv => (now - kv.Value).TotalSeconds > 10).Select(kv => kv.Key).ToList();
            foreach (var key in expired) _recentRequests.TryRemove(key, out _);
        }

        try
        {
            Location loc = _locationService.GetLocation(requestInfo.LocationCode);
            if (loc == null)
            {
                throw new InvalidRequestException($"#{requestInfo.LocationCode} {HttpContext.Translate("不存在")}！");
            }

            // 优先：流程引擎（条件匹配模板）
            var template = await _flowEngine.MatchTemplateAsync(
                loc.RequestType ?? "", Cst.PhaseRequest, null, loc.Tag);

            if (template != null)
            {
                using var flowScope = _scopeFactory.CreateScope();
                var flowDb = flowScope.ServiceProvider.GetRequiredService<WmsDbContext>();
                // 确保起始位置被 DbContext 跟踪（避免 MemoryCache 缓存的游离实体问题）
                var trackedLoc = await flowDb.Locations.FindAsync(loc.LocationId) ?? loc;

                WcsResult? lastFlowResult = null;
                var requestType = loc.RequestType ?? "";

                if (requestType == Cst.入库)
                {
                    // ====== 标准入库：不循环，单次执行 ======
                    var flowContext = new FlowContext(flowDb)
                    {
                        Phase = Cst.PhaseRequest,
                        WcsRequest = requestInfo,
                        StartLocation = trackedLoc,
                        CurrentContainerCode = requestInfo.ContainerCode?.FirstOrDefault(cc => !string.IsNullOrWhiteSpace(cc)),
                        BusinessType = "WcsRequest",
                        BusinessId = requestInfo.LocationCode
                    };
                    lastFlowResult = await _flowEngine.ExecuteAsync(template, flowContext);

                    // 失败：记录日志并返回
                    if (lastFlowResult.resultcode != ResultCodeTypes.一)
                    {
                        stopwatch.Stop();
                        log.ResponseBody = Truncate(JsonConvert.SerializeObject(lastFlowResult), 8000);
                        log.Success = false;
                        log.DurationMs = stopwatch.ElapsedMilliseconds;
                        log.Comment = lastFlowResult.msg;
                        _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(log));
                        return lastFlowResult;
                    }
                    // 成功：fall through 到下方统一成功处理
                }
                else if (requestType == Cst.叠盘)
                {
                    // ====== 叠盘：不循环，单次执行（所有容器码一起处理）======
                    var flowContext = new FlowContext(flowDb)
                    {
                        Phase = Cst.PhaseRequest,
                        WcsRequest = requestInfo,
                        StartLocation = trackedLoc,
                        CurrentContainerCode = requestInfo.ContainerCode?.FirstOrDefault(cc => !string.IsNullOrWhiteSpace(cc)),
                        BusinessType = "WcsRequest",
                        BusinessId = requestInfo.LocationCode
                    };
                    lastFlowResult = await _flowEngine.ExecuteAsync(template, flowContext);

                    // 失败：记录日志并返回
                    if (lastFlowResult.resultcode != ResultCodeTypes.一)
                    {
                        stopwatch.Stop();
                        log.ResponseBody = Truncate(JsonConvert.SerializeObject(lastFlowResult), 8000);
                        log.Success = false;
                        log.DurationMs = stopwatch.ElapsedMilliseconds;
                        log.Comment = lastFlowResult.msg;
                        _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(log));
                        return lastFlowResult;
                    }
                    // 成功：fall through 到下方统一成功处理
                }
                else if (requestType == Cst.排废)
                {
                    // ====== 排废验证：单次执行，所有容器码一起处理 ======
                    var flowContext = new FlowContext(flowDb)
                    {
                        Phase = Cst.PhaseRequest,
                        WcsRequest = requestInfo,
                        StartLocation = trackedLoc,
                        BusinessType = "WcsRequest",
                        BusinessId = requestInfo.LocationCode
                    };
                    lastFlowResult = await _flowEngine.ExecuteAsync(template, flowContext);

                    // 失败：记录日志并返回
                    if (lastFlowResult.resultcode != ResultCodeTypes.一)
                    {
                        stopwatch.Stop();
                        log.ResponseBody = Truncate(JsonConvert.SerializeObject(lastFlowResult), 8000);
                        log.Success = false;
                        log.DurationMs = stopwatch.ElapsedMilliseconds;
                        log.Comment = lastFlowResult.msg;
                        _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(log));
                        return lastFlowResult;
                    }
                    // 成功：从 context.Data 提取排废结果
                    if (flowContext.Data.TryGetValue("WasteDisposalResults", out var wasteResults))
                    {
                        lastFlowResult = ApiResultHelper.WcsSuccess("排废验证完成", ResultCodeTypes.一, 1, data: wasteResults);
                    }
                }
                else if (requestType == Cst.排废更新)
                {
                    // ====== 排废完成：单次执行，所有容器码一起处理 ======
                    var flowContext = new FlowContext(flowDb)
                    {
                        Phase = Cst.PhaseRequest,
                        WcsRequest = requestInfo,
                        StartLocation = trackedLoc,
                        BusinessType = "WcsRequest",
                        BusinessId = requestInfo.LocationCode
                    };
                    lastFlowResult = await _flowEngine.ExecuteAsync(template, flowContext);

                    if (lastFlowResult.resultcode != ResultCodeTypes.一)
                    {
                        stopwatch.Stop();
                        log.ResponseBody = Truncate(JsonConvert.SerializeObject(lastFlowResult), 8000);
                        log.Success = false;
                        log.DurationMs = stopwatch.ElapsedMilliseconds;
                        log.Comment = lastFlowResult.msg;
                        _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(log));
                        return lastFlowResult;
                    }
                    // 成功：从 context.Data 提取排废完成结果
                    if (flowContext.Data.TryGetValue("WasteDisposalCaptureResults", out var captureResults))
                    {
                        lastFlowResult = ApiResultHelper.WcsSuccess("排废完成", ResultCodeTypes.一, 1, data: captureResults);
                    }
                }
                else
                {
                    // ====== 入库双叉/其他：循环遍历容器码 ======
                    Location? firstTarget = null;
                    string? sharedLocationGroup = (requestInfo.ContainerCode?.Length > 1)
                        ? Wms.Core.Domain.Extensions.StringExtensions.GenerateTimeStamp()
                        : null;

                    foreach (var cc in requestInfo.ContainerCode ?? [])
                    {
                        if (string.IsNullOrWhiteSpace(cc)) continue;

                        var flowContext = new FlowContext(flowDb)
                        {
                            Phase = Cst.PhaseRequest,
                            WcsRequest = requestInfo,
                            StartLocation = trackedLoc,
                            CurrentContainerCode = cc,
                            BusinessType = "WcsRequest",
                            BusinessId = requestInfo.LocationCode
                        };

                        if (firstTarget != null)
                            flowContext.Data["ReferenceLocation"] = firstTarget;
                        if (sharedLocationGroup != null)
                            flowContext.Data["SharedLocationGroup"] = sharedLocationGroup;

                        var flowResult = await _flowEngine.ExecuteAsync(template, flowContext);
                        lastFlowResult = flowResult;

                        // 任一容器失败则立即返回
                        if (flowResult.resultcode != ResultCodeTypes.一)
                        {
                            stopwatch.Stop();
                            log.ResponseBody = Truncate(JsonConvert.SerializeObject(flowResult), 8000);
                            log.Success = false;
                            log.DurationMs = stopwatch.ElapsedMilliseconds;
                            log.Comment = flowResult.msg;
                            _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(log));
                            return flowResult;
                        }

                        firstTarget ??= flowContext.TargetLocation;
                    }
                }

                // 所有容器处理成功（入库/双叉共用）
                stopwatch.Stop();
                log.ResponseBody = Truncate(JsonConvert.SerializeObject(lastFlowResult), 8000);
                log.Success = true;
                log.DurationMs = stopwatch.ElapsedMilliseconds;
                log.Comment = lastFlowResult?.msg;
                _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(log));

                return lastFlowResult ?? ApiResultHelper.WcsSuccess("流程引擎处理成功", ResultCodeTypes.一, 1);
            }

            // 后备：策略分发（按 Location.RequestType 找到对应 Handler）
            var handler = _requestHandlers.FirstOrDefault(h => h.RequestType == loc.RequestType);
            if (handler != null)
            {
                var handlerResult = await handler.HandleAsync(requestInfo, loc);

                // 异步写入接口日志
                stopwatch.Stop();
                log.ResponseBody = Truncate(JsonConvert.SerializeObject(handlerResult), 8000);
                log.Success = handlerResult.resultcode == ResultCodeTypes.一;
                log.DurationMs = stopwatch.ElapsedMilliseconds;
                log.Comment = handlerResult.msg;
                _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(log));

                return handlerResult;
            }

            // 无匹配 Handler：返回默认响应（兼容旧逻辑）
            _logger.LogWarning("[WcsController] 未匹配的请求类型: {RequestType}, 位置: {Location}",
                loc.RequestType, loc.LocationCode);
            _wcsResult.msg = $"位置: {requestInfo.LocationCode}，未匹配的请求类型: {loc.RequestType}";
            _wcsResult.resultcode = ResultCodeTypes.数据异常;
        }
        catch (Exception ex)
        {
            if (ex.InnerException == null)
            {
                if (ex.Message.Contains("已生成任务") || ex.Message.Contains("任务正在执行") || ex.Message.Contains("托盘移动中"))
                {
                    _wcsResult.resultcode = ResultCodeTypes.任务重复;
                }
                else if (ex.Message.Contains("两个批次不同"))
                {
                    _wcsResult.resultcode = ResultCodeTypes.排废批次不同;
                }
                else
                {
                    _wcsResult.resultcode = ResultCodeTypes.数据异常;
                }
            }
            else
            {
                _wcsResult.resultcode = ResultCodeTypes.程序异常;
            }
            _wcsResult.msg = $"位置: {requestInfo.LocationCode}，托盘码：{containerCode}，操作失败," + _wcsResult.resultcode;

            // SignalR 推送前端（fire-and-forget，避免延长 WCS 接口响应时间）
            _ = Task.Run(async () =>
            {
                try
                {
                    await _hub.Clients.All.SendAsync("ReceiveWcsError", new
                    {
                        locationCode = requestInfo.LocationCode,
                        containerCode = containerCode,
                        errorCode = _wcsResult.resultcode.ToString(),
                        errorMessage = _wcsResult.msg,
                        exceptionMessage = ex.Message,
                        hasInnerException = ex.InnerException != null,
                        timestamp = DateTime.UtcNow.ToString("o")
                    });
                }
                catch (Exception hubEx)
                {
                    _logger.LogWarning(hubEx, "[WcsController] SignalR 推送 ReceiveWcsError 失败");
                }
            });
        }

        // 异步写入接口日志
        stopwatch.Stop();
        log.ResponseBody = Truncate(JsonConvert.SerializeObject(_wcsResult), 8000);
        log.Success = _wcsResult.resultcode == ResultCodeTypes.一;
        log.DurationMs = stopwatch.ElapsedMilliseconds;
        log.Comment = _wcsResult.msg;
        _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(log));

        return _wcsResult;
    }

    /// <summary>
    /// 9. 生成装盘电芯数据
    /// </summary>
    /// <param name="number">数量</param>
    /// <param name="month">月</param>
    /// <param name="day">日</param>
    /// <param name="start">起始号</param>
    /// <returns></returns>
    [HttpPost("CreateBattery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public WcsResult CreateBattery(int number, int month, int day, int start)
    {
        var battery = _unitloadService.GenerateBatteryBarcodes(number, month, day, start);

        WcsResult _wcsResult = new WcsResult();
        _wcsResult.resultcode = ResultCodeTypes.一;
        _wcsResult.currentoperation = 4;
        _wcsResult.msg = JsonConvert.SerializeObject(battery);
        return _wcsResult;
    }

    /// <summary>
    /// WCS 任务状态同步（内部接口，供定时任务 HTTP 模式调用）
    /// </summary>
    [HttpPost("internal/sync")]
    public async Task<IActionResult> SyncTaskStatus()
    {
        if (_wcsTaskSyncService == null)
            return StatusCode(500, new { status = false, msg = "WcsTaskSyncService 未注册" });

        try
        {
            await _wcsTaskSyncService.SyncStatusAsync();
            await _wcsTaskSyncService.RetryUnsentTasksAsync();
            return Ok(new { status = true, msg = "WCS 同步完成" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WCS 任务同步失败");
            return StatusCode(500, new { status = false, msg = "同步失败，请检查服务端日志" });
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    private async Task SaveInterfaceLogAsync(InterfaceLog log)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var logDb = scope.ServiceProvider.GetRequiredService<WmsLogDbContext>();
            logDb.InterfaceLogs.Add(log);
            await logDb.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入接口日志失败: {Message}", ex.Message);
        }
    }


}
