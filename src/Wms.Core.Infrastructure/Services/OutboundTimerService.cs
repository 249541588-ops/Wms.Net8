using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wms.Core.Application.Handlers.WcsRequest;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Extensions;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Handlers.WcsRequest;
using Wms.Core.Infrastructure.Persistence;
using WcsRequestDto = Wms.Core.Application.DTOs.WcsRequest;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 出库定时器服务 — 负责定时检查满足条件的托盘并创建出库任务下发给 WCS
/// </summary>
public class OutboundTimerService : IOutboundTimerService
{
    private readonly WmsDbContext _db;
    private readonly IBasicDictionaryService _dictService;
    private readonly IWcsTaskBridge _wcsBridge;
    private readonly IMesClient _mesClient;
    private readonly IHangKeClient _hangkeClient;
    private readonly HangKeClientOptions _hangkeOptions;
    private readonly ILogger<OutboundTimerService> _logger;
    private readonly IEnumerable<IWcsRequestHandler> _requestHandlers;
    private readonly IDistributedLockService? _distributedLock;

    /// <summary>
    ///
    /// </summary>
    /// <param name="db"></param>
    /// <param name="dictService"></param>
    /// <param name="wcsBridge"></param>
    /// <param name="mesClient"></param>
    /// <param name="hangkeClient"></param>
    /// <param name="hangkeOptions"></param>
    /// <param name="logger"></param>
    /// <param name="requestHandlers">WCS 请求处理器集合（按 RequestType 路由）</param>
    /// <param name="distributedLock">分布式锁服务（可选，Redis 未启用时为 null）</param>
    /// <exception cref="ArgumentNullException"></exception>
    public OutboundTimerService(
        WmsDbContext db,
        IBasicDictionaryService dictService,
        IWcsTaskBridge wcsBridge,
        IMesClient mesClient,
        IHangKeClient hangkeClient,
        IOptions<HangKeClientOptions> hangkeOptions,
        ILogger<OutboundTimerService> logger,
        IEnumerable<IWcsRequestHandler> requestHandlers,
        IDistributedLockService? distributedLock = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _dictService = dictService ?? throw new ArgumentNullException(nameof(dictService));
        _wcsBridge = wcsBridge ?? throw new ArgumentNullException(nameof(wcsBridge));
        _mesClient = mesClient ?? throw new ArgumentNullException(nameof(mesClient));
        _hangkeClient = hangkeClient ?? throw new ArgumentNullException(nameof(hangkeClient));
        _hangkeOptions = hangkeOptions?.Value ?? throw new ArgumentNullException(nameof(hangkeOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _requestHandlers = requestHandlers ?? throw new ArgumentNullException(nameof(requestHandlers));
        _distributedLock = distributedLock;
    }

    /// <summary>
    /// 执行高温浸润出库任务
    /// </summary>
    public async Task<WcsResult> ExecuteGaowenOutboundAsync()
    {
        return await ExecuteTimedOutboundCoreAsync(
            "OUTBOUNDGAOWEN",
            "PROCESSTIME_1",
            Unitload_Enum.CurrentOperation.高温浸润.ToString(),
            "高温浸润",
            "Gaowen",
            "OutboundTimer:GaowenOutbound");
    }

    /// <summary>
    /// 执行一天出库任务
    /// </summary>
    public async Task<WcsResult> ExecuteOnedayOutboundAsync()
    {
        return await ExecuteTimedOutboundCoreAsync(
            "OUTBOUNDONEDAY",
            "PROCESSTIME_2",
            Unitload_Enum.CurrentOperation.常温一天.ToString(),
            "常温一天",
            "Oneday",
            "OutboundTimer:OnedayOutbound");
    }

    /// <summary>
    /// 执行七天出库任务
    /// </summary>
    public async Task<WcsResult> ExecuteSevenDayOutboundAsync()
    {
        return await ExecuteTimedOutboundCoreAsync(
            "OUTBOUNDSEVENDAY",
            "PROCESSTIME_3",
            Unitload_Enum.CurrentOperation.常温七天.ToString(),
            "常温七天",
            "SevenDay",
            "OutboundTimer:SevenDayOutbound");
    }

    /// <summary>
    /// 执行成品出库任务
    /// </summary>
    public async Task<WcsResult> ExecuteFinishedProductOutboundAsync()
    {
        return await ExecuteTimedOutboundCoreAsync(
            "OUTBOUNDFINISHPRODUCT",
            "PROCESSTIME_4",
            Unitload_Enum.CurrentOperation.成品.ToString(),
            "成品",
            "FinishedProduct",
            "OutboundTimer:FinishedProductOutbound");
    }

    /// <summary>
    /// 定时出库核心逻辑（高温浸润/常温一天/常温七天/成品 共用）
    /// </summary>
    /// <param name="outboundDictNo">出库口字典编码，如 OUTBOUNDGAOWEN</param>
    /// <param name="processTimeDictNo">浸润时间字典编码，如 PROCESSTIME_1</param>
    /// <param name="cutOp">CurrentOperation 枚举名称字符串</param>
    /// <param name="bizName">业务名称（中文），用于返回消息</param>
    /// <param name="logPrefix">日志前缀（英文，无方括号，如 Gaowen）</param>
    /// <param name="lockKey">分布式锁键，如 OutboundTimer:GaowenOutbound</param>
    private async Task<WcsResult> ExecuteTimedOutboundCoreAsync(
        string outboundDictNo,
        string processTimeDictNo,
        string cutOp,
        string bizName,
        string logPrefix,
        string lockKey)
    {
        return await ExecuteWithLockAsync(lockKey, logPrefix, async () =>
        {
            // 1a. 获取出库口 LocationCode 列表
            var outboundPorts = _dictService.GetItemsByNo(outboundDictNo);
            if (outboundPorts == null || outboundPorts.Count == 0)
                return ApiResultHelper.WcsFail($"未配置出库口 {outboundDictNo}", ResultCodeTypes.数据异常, -1);

            // 1b. 获取浸润时间（分钟）
            var processTime = _dictService.GetByNo(processTimeDictNo);
            if (processTime == null || !int.TryParse(processTime.Value, out int minutes))
                return ApiResultHelper.WcsFail($"未配置浸润时间 {processTimeDictNo}", ResultCodeTypes.数据异常, -1);
            var cutoffTime = DateTime.Now.AddMinutes(-minutes);

            _logger.LogInformation("[{Prefix}] 开始{BizName}出库: 出库口={Count}个, 浸润时间={Minutes}分钟",
                logPrefix, bizName, outboundPorts.Count, minutes);

            // 2. 遍历每个出库口
            foreach (var portItem in outboundPorts)
            {
                var locationCode = portItem.Value?.Trim();
                if (string.IsNullOrEmpty(locationCode)) continue;

                try
                {
                    await ProcessOutboundPortAsync(locationCode, cutoffTime, cutOp);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Prefix}] 出库口处理失败: {LocationCode}", logPrefix, locationCode);
                }
            }

            return ApiResultHelper.WcsSuccess($"{bizName}出库完成", ResultCodeTypes.一, 1);
        }, bizName);
    }

    /// <summary>
    /// 推送待上传的 MES 信息（UploadMesInfo 队列消费）
    /// </summary>
    public async Task<WcsResult> SendPendingMesAsync()
    {
        const int batchSize = 100; // 单次最多处理条数，防止积压过多时单次执行过长
        try
        {
            // 取待上传记录（mtime 升序；新记录 mtime=NULL 排最前，优先于重试记录）
            var pending = await _db.UploadMesInfos
                .Where(x => x.MesIsFlag == 1)
                .OrderBy(x => x.mtime)
                .Take(batchSize)
                .ToListAsync();

            if (pending.Count == 0)
                return ApiResultHelper.WcsSuccess("没有需要上传MES的数据", ResultCodeTypes.一, 1);

            int ok = 0, fail = 0, skip = 0;
            foreach (var mes in pending)
            {
                try
                {
                    // 逐条独立处理 + 独立提交：单条异常不影响其余记录
                    if ((mes.MestextInfo?.Length ?? 0) <= 2)
                    {
                        _logger.LogWarning("[MesSend] 数据长度<=2，跳过: Id={Id}", mes.Id);
                        mes.MesIsFlag = 2;
                        mes.MesMsg = "数据长度小于等于2";
                        mes.mtime = DateTime.Now;
                        skip++;
                    }
                    else if (mes.ErrCount >= 5)
                    {
                        _logger.LogWarning("[MesSend] 错误次数超限，标记失败: Id={Id}, ErrCount={ErrCount}", mes.Id, mes.ErrCount);
                        mes.MesIsFlag = 3;
                        mes.MesMsg = $"数据上传错误次数{mes.ErrCount}";
                        mes.mtime = DateTime.Now;
                        skip++;
                    }
                    else
                    {
                        // HTTP 调用单独 try-catch：网络异常/超时也按"业务失败"处理，享受 ErrCount+退避
                        MesResult result;
                        try
                        {
                            result = await _mesClient.PushMesInfoAsync(mes.MestextInfo);
                        }
                        catch (Exception exHttp)
                        {
                            _logger.LogError(exHttp, "[MesSend] HTTP 异常按失败处理: Id={Id}", mes.Id);
                            result = new MesResult { status = false, message = "HTTP异常:" + exHttp.Message };
                        }

                        if (!result.status)
                        {
                            _logger.LogError("[MesSend] 上传失败: Id={Id}, Code={Code}, Msg={Msg}", mes.Id, result.code, result.message);
                            mes.MesMsg = result.message?.Length > 1000 ? result.message[..1000] : result.message;
                            mes.ErrCount += 1;
                            mes.mtime = DateTime.Now.AddMinutes(10);   // 延后10分钟重试
                            fail++;
                        }
                        else
                        {
                            mes.MesIsFlag = 2;
                            mes.MesMsg = result.message;
                            mes.mtime = DateTime.Now;
                            _logger.LogInformation("[MesSend] 上传成功: Id={Id}, Msg={Msg}", mes.Id, result.message);
                            ok++;
                        }
                    }
                    await _db.SaveChangesAsync();   // 每条独立提交，避免一条回滚连带其余
                }
                catch (Exception exRec)
                {
                    // 仅回滚当前一条记录的内存修改，绝不 Clear() 整个 tracker（否则后续记录会被静默丢弃）
                    _logger.LogError(exRec, "[MesSend] 单条处理异常: Id={Id}", mes.Id);
                    fail++;
                    var entry = _db.Entry(mes);
                    entry.CurrentValues.SetValues(entry.OriginalValues);   // 还原到加载时的值
                }
            }

            return ApiResultHelper.WcsSuccess(
                $"MES上传处理完成: 成功{ok} 失败{fail} 跳过{skip}", ResultCodeTypes.一, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MesSend] MES 上传处理异常");
            return ApiResultHelper.WcsFail($"MES上传处理异常: {ex.Message}", ResultCodeTypes.程序异常, -1);
        }
    }

    /// <summary>
    /// 杭可申请取盘（扫描待取盘货位，请求杭可设备取盘）
    /// </summary>
    public async Task<WcsResult> PickPendingPalletAsync()
    {
        if (!_hangkeOptions.Enable)
            return ApiResultHelper.WcsSuccess("杭可通知未启用", ResultCodeTypes.一, 1);

        try
        {
            // 取待取盘货位（HKPosintionState=7 杭可作业完成, HKPosintionCK=0 未取盘）
            var locations = await _db.Locations
                .Where(l => l.HKPosintionState == 7 && l.HKPosintionCK == 0)
                .OrderBy(l => l.ModifiedTime)
                .Take(20)
                .ToListAsync();

            if (locations.Count == 0)
                return ApiResultHelper.WcsSuccess("未找到待取盘货位", ResultCodeTypes.一, 1);

            foreach (var loc in locations)
            {
                // 找该货位上的托盘
                var unitload = await _db.Unitloads
                    .FirstOrDefaultAsync(u => u.LocationId == loc.LocationId);

                if (unitload == null || string.IsNullOrWhiteSpace(unitload.ContainerCode))
                {
                    _logger.LogWarning("[PickPallet] 货位 {LocCode} 无货载，跳过", loc.LocationCode);
                    continue;   // 空货位直接跳过，找下一个（旧版用 mtime 延后，新版靠遍历绕开）
                }

                // 请求杭可取盘
                ResultInfo result;
                try
                {
                    result = await _hangkeClient.InOutNotifyAsync(
                        loc.AnotherCode ?? "", unitload.ContainerCode, InOutType_Enum.申请取盘);
                }
                catch (Exception exHttp)
                {
                    _logger.LogError(exHttp, "[PickPallet] 杭可接口异常: 货位={LocCode}, 托盘={ContainerCode}",
                        loc.LocationCode, unitload.ContainerCode);
                    return ApiResultHelper.WcsFail($"杭可接口异常: {exHttp.Message}", ResultCodeTypes.程序异常, -1);
                }

                if (result.ResultCode == 1)
                {
                    loc.HKPosintionCK = 1;
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("[PickPallet] 取盘成功: 货位={LocCode}, 托盘={ContainerCode}",
                        loc.LocationCode, unitload.ContainerCode);
                    return ApiResultHelper.WcsSuccess(
                        $"取盘成功: {loc.LocationCode}", ResultCodeTypes.一, 1);
                }
                else
                {
                    _logger.LogWarning("[PickPallet] 取盘失败: 货位={LocCode}, 托盘={ContainerCode}, Code={Code}, Msg={Msg}",
                        loc.LocationCode, unitload.ContainerCode, result.ResultCode, result.ResultMessage);
                    return ApiResultHelper.WcsSuccess(
                        $"取盘失败: {loc.LocationCode} {result.ResultMessage}", ResultCodeTypes.一, 1);
                }
            }

            return ApiResultHelper.WcsSuccess("待取盘货位均无货载", ResultCodeTypes.一, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PickPallet] 取盘处理异常");
            return ApiResultHelper.WcsFail($"取盘处理异常: {ex.Message}", ResultCodeTypes.程序异常, -1);
        }
    }

    /// <summary>
    /// 杭可申请移库（扫描 HKPosintionState=8 的货位，触发库内移动）
    /// </summary>
    /// <remarks>
    /// 流程：获取分布式锁 → 扫描待移库货位 → 按 Cst.移库 路由到 MoveRequestHandler → 释放锁。
    /// Redis 未启用时降级为无锁执行（仅警告日志）；handler 内部自管事务与并发校验。
    /// </remarks>
    public async Task<WcsResult> MovePendingAsync()
    {
        return await ExecuteWithLockAsync("OutboundTimer:MovePending", "Move", async () =>
        {
            var (ok, fail, skip) = await MovePendingCoreAsync();
            return ApiResultHelper.WcsSuccess(
                $"移库处理完成: 成功{ok} 失败{fail} 跳过{skip}", ResultCodeTypes.一, 1);
        }, "移库循环");
    }

    /// <summary>
    /// 移库核心循环（实际业务逻辑）
    /// </summary>
    /// <returns>(成功, 失败, 跳过)</returns>
    private async Task<(int ok, int fail, int skip)> MovePendingCoreAsync()
    {
        // AsNoTracking：service 只读，不污染 change tracker（DbContext 与 handler 共享）
        var locations = await _db.Locations.AsNoTracking()
            .Where(l => l.HKPosintionState == 8 && !l.OutboundDisabled)
            .OrderBy(l => l.ModifiedTime)
            .Take(20)
            .ToListAsync();

        if (locations.Count == 0)
        {
            _logger.LogInformation("[Move] 未找到待移库货位");
            return (0, 0, 0);
        }

        // 按 Cst.移库 路由到 MoveRequestHandler（与 WcsController.cs 中相同 dispatch 方式）
        var moveHandler = _requestHandlers.FirstOrDefault(h => h.RequestType == Cst.移库);
        if (moveHandler == null)
        {
            _logger.LogError("[Move] 未注册移库请求处理器 (Cst.移库)");
            return (0, 0, locations.Count);
        }

        int ok = 0, fail = 0, skip = 0;
        foreach (var loc in locations)
        {
            try
            {
                // 货位上的第一个非移动中托盘（保持原方法 FirstOrDefault 语义）
                var unitload = await _db.Unitloads.AsNoTracking()
                    .Where(u => u.LocationId == loc.LocationId && u.BeingMoved != true)
                    .FirstOrDefaultAsync();
                if (unitload == null || string.IsNullOrWhiteSpace(unitload.ContainerCode))
                {
                    _logger.LogDebug("[Move] 货位 {LocCode} 无可移库托盘，跳过", loc.LocationCode);
                    skip++;
                    continue;
                }

                var request = new WcsRequestDto
                {
                    LocationCode = loc.LocationCode,
                    ContainerCode = new[] { unitload.ContainerCode }
                };

                // handler 内部自管事务；二次并发校验在 handler 内完成
                var result = await moveHandler.HandleAsync(request, loc);
                if (result.success)
                {
                    ok++;
                    _logger.LogInformation("[Move] 移库任务已下发: 货位={LocCode}, 托盘={Container}",
                        loc.LocationCode, unitload.ContainerCode);
                }
                else
                {
                    fail++;
                    _logger.LogWarning("[Move] 移库失败: 货位={LocCode}, 托盘={Container}, Msg={Msg}",
                        loc.LocationCode, unitload.ContainerCode, result.msg);
                }
            }
            catch (Exception exLoc)
            {
                fail++;
                _logger.LogError(exLoc, "[Move] 货位处理异常: {LocCode}", loc.LocationCode);
                // 不 rethrow，继续下一条
            }
        }

        _logger.LogInformation("[Move] 循环结束: 成功{Ok} 失败{Fail} 跳过{Skip}", ok, fail, skip);
        return (ok, fail, skip);
    }

    /// <summary>
    /// 处理单个出库口
    /// </summary>
    private async Task ProcessOutboundPortAsync(string locationCode, DateTime cutoffTime,string cutOp)
    {
        _logger.LogInformation("[{cutOp}] 处理出库口: {LocationCode}", cutOp, locationCode);

        var resolved = await ResolveOutboundPortLanewaysAsync(locationCode, cutOp);
        if (resolved == null) return;

        var (portLocation, laneways) = resolved.Value;

        // 遍历每个 Laneway
        foreach (var laneway in laneways)
        {
            try
            {
                await ProcessLanewayAsync(laneway, cutoffTime, cutOp, portLocation.LocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{cutOp}] 巷道处理失败: LanewayId={LanewayId}", cutOp, laneway.LanewayId);
            }
        }
    }

    /// <summary>
    /// 处理单个巷道的出库批次
    /// </summary>
    private async Task ProcessLanewayAsync(Domain.Entities.Warehouse.Laneway laneway, DateTime cutoffTime, string cutOp, int endLocationId)
    {
        // 3a. 预过滤 OutboundBatch（DB 服务端过滤）
        var batches = await _db.OutboundBatches.AsNoTracking()
            .Where(ob => ob.LanewayId == laneway.LanewayId
                && ob.CurrentOperation == cutOp
                && ob.Status == 1
                && ob.ErrorCount <= 3
                && ob.QuantityDelivered < ob.QuantityRequired)
            .ToListAsync();

        _logger.LogInformation("[Gaowen] 巷道 LanewayId={LanewayId}: 符合条件 OutboundBatches={BatchCount}个",
            laneway.LanewayId, batches.Count);

        foreach (var ob in batches)
        {
            try
            {
                await ProcessBatchAsync(ob, cutoffTime, endLocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Gaowen] Batch 处理异常: Id={Id}, Batch={Batch}", ob.Id, ob.Batch);
            }
        }
    }

    /// <summary>
    /// 处理单个出库批次
    /// </summary>
    private async Task ProcessBatchAsync(Domain.Entities.Outbound.OutboundBatch ob, DateTime cutoffTime, int endLocationId)
    {
        int remaining = ob.QuantityRequired - ob.QuantityDelivered;

        // 3b. UnitloadItem 精确匹配（Batch + MaterialId + OperationNumber + IsAdvance + IsSupplement）
        var query = _db.UnitloadItems.AsNoTracking()
            .Where(ui => ui.MaterialId == ob.MaterialId);

        if (!string.IsNullOrWhiteSpace(ob.Batch))
            query = query.Where(ui => ui.Batch == ob.Batch);
        if (!string.IsNullOrWhiteSpace(ob.xLevel))
            query = query.Where(ui => ui.xLevel == ob.xLevel);
        if (ob.OperationNumber.HasValue)
            query = query.Where(ui => ui.OperationNumber == ob.OperationNumber);
        if (ob.IsAdvance != 0)
            query = query.Where(ui => ui.IsAdvance == ob.IsAdvance);
        if (ob.IsSupplement != 0)
            query = query.Where(ui => ui.IsSupplement == ob.IsSupplement);
       
        var unitloadIds = await query
            .Select(ui => ui.UnitloadId)
            .Distinct()
            .ToListAsync();

        if (unitloadIds.Count == 0)
        {
            _logger.LogWarning("[Gaowen] Batch Id={Id} 未匹配到 UnitloadItem: Batch={Batch}, MaterialId={MaterialId}",
                ob.Id, ob.Batch, ob.MaterialId);
            await HandleBatchNoMatchAsync(ob);
            return;
        }

        // 3c. 筛选 Unitload（CurrentOperation + 时间 + 状态 + 数量上限）
        var unitloads = await _db.Unitloads.AsNoTracking()
            .Where(u => unitloadIds.Contains(u.UnitloadId)
                && u.Location.Rack.LanewayId == ob.LanewayId
                && u.Location.LocationType == Location_Enum.LocationType.R.ToString()
                && u.Location.xExists == true
                && u.Location.OutboundDisabled == false
                && u.CurrentLocationTime < cutoffTime
                && u.CurrentOperation == ob.CurrentOperation
                && u.BeingMoved != true
                && u.Allocated != true
                && u.LocationId.HasValue)
            .Include(u => u.Location)
            .OrderBy(u => u.CurrentLocationTime)
            .Take(remaining)
            .ToListAsync();

        if (unitloads.Count == 0)
        {
            _logger.LogWarning("[Gaowen] Batch Id={Id} 有 UnitloadItem={ItemCount}个但无满足条件的 Unitload（浸润时间未到/正在移动/已分配）, cutoff={Cutoff}",
                ob.Id, unitloadIds.Count, cutoffTime.ToString("yyyy-MM-dd HH:mm:ss"));
            await HandleBatchNoMatchAsync(ob);
            return;
        }

        _logger.LogInformation("[Gaowen] Batch Id={Id}, Batch={Batch}: 匹配 Unitload={Count}个（剩余需求 {Remaining}）",
            ob.Id, ob.Batch, unitloads.Count, remaining);

        // 3e. 按 LocationId 分组，每个库位创建一个 TransTask（同库位多托盘一起搬运）
        var grouped = unitloads.GroupBy(u => u.LocationId!.Value);
        int successCount = 0;
        foreach (var group in grouped)
        {
            try
            {
                var matchedInGroup = group.ToList();
                if (await ProcessLocationGroupAsync(matchedInGroup, ob.Batch, endLocationId, ob))
                    successCount += matchedInGroup.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Gaowen] Location Group 出库失败: LocationId={LocationId}", group.Key);
            }
        }

        // 3f. 仅处理全失败场景的 ErrorCount（成功场景已在 ProcessLocationGroupAsync 事务内更新）
        if (successCount == 0)
        {
            var trackedBatch = await _db.OutboundBatches.FindAsync(ob.Id);
            if (trackedBatch != null)
            {
                trackedBatch.ErrorCount++;
                if (trackedBatch.ErrorCount > 3)
                    trackedBatch.Status = 0;
                await _db.SaveChangesAsync();
                _logger.LogWarning("[Gaowen] Batch 全部分组失败: Id={Id}, ErrorCount={ErrorCount}",
                    ob.Id, trackedBatch.ErrorCount);
            }
        }
    }

    /// <summary>
    /// 处理无匹配 Unitload 的情况 — 递增 ErrorCount
    /// </summary>
    private async Task HandleBatchNoMatchAsync(Domain.Entities.Outbound.OutboundBatch ob)
    {
        var tb = await _db.OutboundBatches.FindAsync(ob.Id);
        if (tb != null)
        {
            tb.ErrorCount++;
            if (tb.ErrorCount > 3)
                tb.Status = 0;
            await _db.SaveChangesAsync();
            _logger.LogWarning("[Gaowen] Batch 无满足条件的 Unitload: Id={Id}, Batch={Batch}, ErrorCount={ErrorCount}",
                ob.Id, ob.Batch, tb.ErrorCount);
        }
    }

    /// <summary>
    /// 处理同库位的一组 Unitload 出库（独立事务，支持多托盘）
    /// </summary>
    /// <remarks>
    /// 同库位多托盘一起搬运：第一个匹配的 Unitload 为主，其余同库位 Unitload 存入 Ext1/Ext2。
    /// Ext1 = 所有容器码（;分隔），Ext2 = 额外 UnitloadId（;分隔）
    /// </remarks>
    private async Task<bool> ProcessLocationGroupAsync(List<Unitload> matchedUnitloads, string? batch, int endLocationId, Domain.Entities.Outbound.OutboundBatch? outboundBatch = null, string? materialCodeFilter = null)
    {
        var primaryUnitload = matchedUnitloads[0];
        var locationId = primaryUnitload.LocationId!.Value;

        await using var tx = await _db.Database.BeginTransactionAsync();

        // 加载主 Unitload（tracked）
        var trackedPrimary = await _db.Unitloads.FindAsync(primaryUnitload.UnitloadId);
        if (trackedPrimary == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        // 二次并发检查
        if (trackedPrimary.BeingMoved == true || trackedPrimary.Allocated == true)
        {
            _logger.LogWarning("[Gaowen] Unitload Id={Id} 二次检查不通过: BeingMoved={BeingMoved}, Allocated={Allocated}",
                primaryUnitload.UnitloadId, trackedPrimary.BeingMoved, trackedPrimary.Allocated);
            await tx.RollbackAsync();
            return false;
        }

        // 查找同库位其他 Unitload（不在移动中、未分配）
        IQueryable<Unitload> additionalQuery = _db.Unitloads
            .Where(u => u.LocationId == locationId
                && u.UnitloadId != primaryUnitload.UnitloadId
                && u.BeingMoved != true
                && u.Allocated != true);

        // 空托盘场景：只拉取同库位同为空托盘的 Unitload，避免有料托盘被误出库
        if (!string.IsNullOrEmpty(materialCodeFilter))
        {
            additionalQuery = additionalQuery
                .Where(u => u.UnitloadItems.Any(ui => ui.Material != null
                    && ui.Material.MaterialCode == materialCodeFilter));
        }

        var additionalUnitloads = await additionalQuery.ToListAsync();

        // 构建 Ext1（所有容器码）和 Ext2（额外 UnitloadId）
        var allCodes = new List<string> { trackedPrimary.ContainerCode ?? "" };
        var additionalIds = new List<int>();
        foreach (var au in additionalUnitloads)
        {
            allCodes.Add(au.ContainerCode ?? "");
            additionalIds.Add(au.UnitloadId);
        }

        var trackedLocation = await _db.Locations.FindAsync(locationId);
        if (trackedLocation == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        var endLocation = await _db.Locations.FindAsync(endLocationId);
        if (endLocation == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        var transTask = new TransTask
        {
            TaskCode = await TaskCodeGenerator.GenerateAsync(_db),
            TaskType = Cst.出库,
            UnitloadId = primaryUnitload.UnitloadId,
            UnitloadCode = trackedPrimary.ContainerCode,
            StartLocationId = locationId,
            EndLocationId = endLocationId,
            ForWcs = true,
            WasSentToWcs = false,
            Ext1 = string.Join(";", allCodes),
            Ext2 = additionalIds.Count > 0 ? string.Join(";", additionalIds) : string.Empty,
            WareHouse = endLocation.AreaName,
            LocationGroup = string.Empty
        };
        transTask.Unitload = trackedPrimary;
        transTask.StartLocation = trackedLocation;
        transTask.EndLocation = endLocation;
        _db.TransTasks.Add(transTask);

        // 标记主 Unitload
        trackedPrimary.BeingMoved = true;
        trackedPrimary.Allocated = true;

        // 标记额外 Unitload
        foreach (var au in additionalUnitloads)
        {
            au.BeingMoved = true;
            au.Allocated = true;
        }

        trackedLocation.OutboundCount++;
        endLocation.InboundCount++;

        // 在事务内更新 OutboundBatch（保证与 Unitload 标记原子提交）
        if (outboundBatch != null)
        {
            var trackedBatch = await _db.OutboundBatches.FindAsync(outboundBatch.Id);
            if (trackedBatch != null)
            {
                trackedBatch.QuantityDelivered += matchedUnitloads.Count;
                trackedBatch.ErrorCount = 0;
                _logger.LogInformation("[Gaowen] Batch 出库成功(事务内): Id={Id}, Batch={Batch}, 本次={Success}, 累计={Total}",
                    outboundBatch.Id, outboundBatch.Batch, matchedUnitloads.Count, trackedBatch.QuantityDelivered);
            }
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // WCS 下发（事务外，失败不影响已提交状态）
        try
        {
            await _wcsBridge.SendTaskAsync(transTask);
            transTask.WasSentToWcs = true;
            transTask.SentToWcsAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _logger.LogInformation("[Gaowen] 出库任务创建: TaskCode={TaskCode}, 主Unitload={Id}, 额外={Additional}个, Batch={Batch}",
                transTask.TaskCode, primaryUnitload.UnitloadId, additionalIds.Count, batch);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Gaowen] WCS 下发失败，开始回滚: TaskCode={TaskCode}, UnitloadId={Id}",
                transTask.TaskCode, primaryUnitload.UnitloadId);

            // 先尝试删除 wcs_tasks 中的孤儿记录（处理 SendTaskAsync 部分成功的场景）
            try
            {
                await _wcsBridge.DeleteTaskAsync(transTask.TaskCode);
            }
            catch (Exception delEx)
            {
                _logger.LogWarning(delEx, "[Gaowen] 回滚时删除 wcs_tasks 失败（可能从未写入）: TaskCode={TaskCode}",
                    transTask.TaskCode);
            }

            try
            {
                await using var rollbackTx = await _db.Database.BeginTransactionAsync();

                // 重置主 Unitload 状态
                var rbPrimary = await _db.Unitloads.FindAsync(primaryUnitload.UnitloadId);
                if (rbPrimary != null)
                {
                    rbPrimary.BeingMoved = false;
                    rbPrimary.Allocated = false;
                }

                // 重置额外 Unitload 状态
                foreach (var addId in additionalIds)
                {
                    var rbAdd = await _db.Unitloads.FindAsync(addId);
                    if (rbAdd != null)
                    {
                        rbAdd.BeingMoved = false;
                        rbAdd.Allocated = false;
                    }
                }

                // 回退 Location 计数
                var rbStartLoc = await _db.Locations.FindAsync(locationId);
                if (rbStartLoc != null && rbStartLoc.OutboundCount > 0)
                    rbStartLoc.OutboundCount--;

                var rbEndLoc = await _db.Locations.FindAsync(endLocationId);
                if (rbEndLoc != null && rbEndLoc.InboundCount > 0)
                    rbEndLoc.InboundCount--;

                // 回退 OutboundBatch 数量（高温浸润场景）
                if (outboundBatch != null)
                {
                    var rbBatch = await _db.OutboundBatches.FindAsync(outboundBatch.Id);
                    if (rbBatch != null && rbBatch.QuantityDelivered >= matchedUnitloads.Count)
                        rbBatch.QuantityDelivered -= matchedUnitloads.Count;
                }

                // 删除已创建的 TransTask
                var rbTask = await _db.TransTasks.FindAsync(transTask.Id);
                if (rbTask != null)
                    _db.TransTasks.Remove(rbTask);

                await _db.SaveChangesAsync();
                await rollbackTx.CommitAsync();

                _logger.LogWarning("[Gaowen] WCS 下发失败已回滚: TaskCode={TaskCode}, 主Unitload={Id}, 额外={Additional}个",
                    transTask.TaskCode, primaryUnitload.UnitloadId, additionalIds.Count);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "[Gaowen] 回滚也失败，数据库可能不一致，需人工检查: TaskCode={TaskCode}",
                    transTask.TaskCode);
            }

            return false;
        }
    }

    /// <summary>
    /// 执行化成出库任务
    /// </summary>
    public async Task<WcsResult> ExecuteHcOutboundAsync()
    {
        return await ExecuteWithLockAsync("OutboundTimer:HcOutbound", "Hc", async () =>
        {
            var outboundPorts = _dictService.GetItemsByNo("OUTBOUNDHC");
            if (outboundPorts == null || outboundPorts.Count == 0)
                return ApiResultHelper.WcsFail("未配置出库口 OUTBOUNDHC", ResultCodeTypes.数据异常, -1);

            _logger.LogInformation("[Hc] 开始化成出库: 出库口={Count}个", outboundPorts.Count);

            int ok = 0, fail = 0, skip = 0;
            foreach (var portItem in outboundPorts)
            {
                var locationCode = portItem.Value?.Trim();
                if (string.IsNullOrEmpty(locationCode)) continue;

                try
                {
                    var (o, f, s) = await ProcessSimpleOutboundPortAsync(locationCode, "化成", "Hc");
                    ok += o; fail += f; skip += s;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Hc] 出库口处理失败: {LocationCode}", locationCode);
                    fail++;
                }
            }

            return ApiResultHelper.WcsSuccess(
                $"化成出库完成: 成功{ok} 失败{fail} 跳过{skip}", ResultCodeTypes.一, 1);
        }, "化成出库");
    }

    /// <summary>
    /// 执行分容出库任务
    /// </summary>
    public async Task<WcsResult> ExecuteFrOutboundAsync()
    {
        return await ExecuteWithLockAsync("OutboundTimer:FrOutbound", "Fr", async () =>
        {
            var outboundPorts = _dictService.GetItemsByNo("OUTBOUNDFR");
            if (outboundPorts == null || outboundPorts.Count == 0)
                return ApiResultHelper.WcsFail("未配置出库口 OUTBOUNDFR", ResultCodeTypes.数据异常, -1);

            _logger.LogInformation("[Fr] 开始分容出库: 出库口={Count}个", outboundPorts.Count);

            int ok = 0, fail = 0, skip = 0;
            foreach (var portItem in outboundPorts)
            {
                var locationCode = portItem.Value?.Trim();
                if (string.IsNullOrEmpty(locationCode)) continue;

                try
                {
                    var (o, f, s) = await ProcessSimpleOutboundPortAsync(locationCode, "分容", "Fr");
                    ok += o; fail += f; skip += s;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Fr] 出库口处理失败: {LocationCode}", locationCode);
                    fail++;
                }
            }

            return ApiResultHelper.WcsSuccess(
                $"分容出库完成: 成功{ok} 失败{fail} 跳过{skip}", ResultCodeTypes.一, 1);
        }, "分容出库");
    }

    /// <summary>
    /// 执行空托盘出库任务
    /// </summary>
    public async Task<WcsResult> ExecuteEmptyOutboundAsync(string exitCode)
    {
        return await ExecuteWithLockAsync($"OutboundTimer:EmptyOutbound:{exitCode}", "Empty", async () =>
        {
            if (string.IsNullOrWhiteSpace(exitCode))
                return ApiResultHelper.WcsFail("出库口编码不能为空", ResultCodeTypes.数据异常, -1);

            var outboundPorts = _dictService.GetItemsByNo(exitCode);
            if (outboundPorts == null || outboundPorts.Count == 0)
                return ApiResultHelper.WcsFail($"未配置出库口 {exitCode}", ResultCodeTypes.数据异常, -1);

            _logger.LogInformation("[Empty] 开始空托盘出库: exitCode={ExitCode}, 出库口={Count}个", exitCode, outboundPorts.Count);

            int ok = 0, fail = 0, skip = 0;
            foreach (var portItem in outboundPorts)
            {
                var locationCode = portItem.Value?.Trim();
                if (string.IsNullOrEmpty(locationCode)) continue;

                try
                {
                    if (await ProcessEmptyOutboundPortAsync(locationCode))
                        ok++;
                    else
                        skip++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Empty] 出库口处理失败: {LocationCode}", locationCode);
                    fail++;
                }
            }

            return ApiResultHelper.WcsSuccess(
                $"空托盘出库完成: 成功{ok} 失败{fail} 跳过{skip}", ResultCodeTypes.一, 1);
        }, "空托盘出库");
    }

    /// <summary>
    /// 处理单个出库口的空托盘出库（检查终点任务 → 解析巷道 → 查空托盘 → 创建任务）
    /// </summary>
    private async Task<bool> ProcessEmptyOutboundPortAsync(string locationCode)
    {
        _logger.LogInformation("[Empty] 处理出库口: {LocationCode}", locationCode);

        var resolved = await ResolveOutboundPortLanewaysAsync(locationCode, "Empty");
        if (resolved == null) return false;

        var (portLocation, laneways) = resolved.Value;

        // 检查出库口（终点）是否已有出库任务（任务完成后归档删除，所以 TransTasks 中的都是未完成的）
        var hasExistingTask = await _db.TransTasks.AsNoTracking()
            .AnyAsync(t => t.TaskType == Cst.出库
                && t.EndLocationId == portLocation.LocationId);
        if (hasExistingTask)
        {
            _logger.LogInformation("[Empty] 出库口 {LocationCode} 已有出库任务，跳过", locationCode);
            return false;
        }

        // 遍历巷道找最早的空托盘
        foreach (var laneway in laneways)
        {
            var query = _db.Unitloads.AsNoTracking()
                .Include(u => u.Location).ThenInclude(l => l.Rack)
                .Include(u => u.UnitloadItems).ThenInclude(ui => ui.Material)
                .Where(u => u.Location.Rack.LanewayId == laneway.LanewayId
                    && u.Location.LocationType == Location_Enum.LocationType.R.ToString()
                    && u.Location.OutboundDisabled == false
                    && u.Location.xExists == true
                    && u.BeingMoved != true
                    && u.Allocated != true
                    && u.LocationId.HasValue
                    && u.UnitloadItems.Any(ui => ui.Material != null
                        && ui.Material.MaterialCode == CommonTypes.空托盘))
                .OrderBy(u => u.CurrentLocationTime);

            var unitload = await query.FirstOrDefaultAsync();

            if (unitload != null)
            {
                _logger.LogInformation("[Empty] 出库口 {LocationCode} 巷道 {LanewayId} 找到空托盘: Container={Container}",
                    locationCode, laneway.LanewayId, unitload.ContainerCode);
                return await ProcessLocationGroupAsync(
                    new List<Unitload> { unitload },
                    null,
                    portLocation.LocationId,
                    null,
                    CommonTypes.空托盘);
            }

            // 失败时输出完整 SQL，便于直接复制到 SSMS 诊断
            _logger.LogWarning("[Empty] 出库口 {LocationCode} 巷道 {LanewayId} 无空托盘 | SQL:\n{Sql}",
                locationCode, laneway.LanewayId, query.ToQueryString());
        }

        _logger.LogWarning("[Empty] 出库口 {LocationCode} 所有巷道无空托盘（已扫描 {LanewayCount} 个巷道）",
            locationCode, laneways.Count);
        return false;
    }

    #region 共用辅助方法

    /// <summary>
    /// 分布式锁模板方法：获取锁 → 执行业务 → 释放锁，自动降级
    /// </summary>
    private async Task<WcsResult> ExecuteWithLockAsync(
        string lockKey, string logPrefix,
        Func<Task<WcsResult>> action,
        string bizName,
        TimeSpan? lockExpiry = null)
    {
        var expiry = lockExpiry ?? TimeSpan.FromMinutes(5);
        string? lockToken = null;

        if (_distributedLock != null)
        {
            try
            {
                if (!await _distributedLock.LockTakeAsync(lockKey, expiry, out lockToken))
                {
                    _logger.LogWarning("[{Prefix}] 上一次{Biz}循环仍在执行，本次跳过", logPrefix, bizName);
                    return ApiResultHelper.WcsSuccess($"上一次{bizName}循环仍在执行，本次跳过", ResultCodeTypes.一, 1);
                }
            }
            catch (Exception exLock)
            {
                _logger.LogWarning(exLock, "[{Prefix}] 分布式锁获取异常，降级为无锁执行", logPrefix);
                lockToken = null;
            }
        }
        else
        {
            _logger.LogWarning("[{Prefix}] IDistributedLockService 未注册（Redis 未启用），无锁执行", logPrefix);
        }

        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Prefix}] {Biz}循环异常", logPrefix, bizName);
            return ApiResultHelper.WcsFail($"{bizName}循环异常: {ex.Message}", ResultCodeTypes.程序异常, -1);
        }
        finally
        {
            if (_distributedLock != null && lockToken != null)
            {
                try { await _distributedLock.LockReleaseAsync(lockKey, lockToken); }
                catch (Exception exRel) { _logger.LogWarning(exRel, "[{Prefix}] 分布式锁释放异常", logPrefix); }
            }
        }
    }

    /// <summary>
    /// 解析出库口关联的巷道列表（Location → Port → LanewayId → Laneway）
    /// </summary>
    private async Task<(Domain.Entities.Warehouse.Location PortLocation, List<Domain.Entities.Warehouse.Laneway> Laneways)?> ResolveOutboundPortLanewaysAsync(string locationCode, string logPrefix)
    {
        var portLocation = await _db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.LocationCode == locationCode);
        if (portLocation == null)
        {
            _logger.LogWarning("[{Prefix}] 出库口不存在: {LocationCode}", logPrefix, locationCode);
            return null;
        }
        if (portLocation.OutboundDisabled || portLocation.OutboundCount >= portLocation.OutboundLimit)
        {
            _logger.LogWarning("[{Prefix}] 出库口不可用: {LocationCode}, Disabled={Disabled}, Count={Count}/{Limit}",
                logPrefix, locationCode, portLocation.OutboundDisabled, portLocation.OutboundCount, portLocation.OutboundLimit);
            return null;
        }

        var portIds = await _db.Ports.AsNoTracking()
            .Where(p => p.KP1 == portLocation.LocationId || p.KP2 == portLocation.LocationId)
            .Select(p => p.Id)
            .ToListAsync();
        if (portIds.Count == 0)
        {
            _logger.LogWarning("[{Prefix}] 未找到关联 Port: LocationId={LocationId}", logPrefix, portLocation.LocationId);
            return null;
        }

        var lanewayIds = await _db.LanewayPorts.AsNoTracking()
            .Where(lp => portIds.Contains(lp.PortId))
            .Select(lp => lp.LanewayId)
            .Distinct()
            .ToListAsync();

        var laneways = await _db.Laneways.AsNoTracking()
            .Where(l => lanewayIds.Contains(l.LanewayId) && l.Offline != true && l.Automated == true)
            .ToListAsync();

        _logger.LogInformation("[{Prefix}] 出库口 {LocationCode}: Port={PortCount}个, Laneway={LanewayCount}个",
            logPrefix, locationCode, portIds.Count, laneways.Count);

        return (portLocation, laneways);
    }

    /// <summary>
    /// 处理化成/分容出库单个出库口（共用：解析出库口 → 遍历巷道）
    /// </summary>
    private async Task<(int ok, int fail, int skip)> ProcessSimpleOutboundPortAsync(
        string locationCode, string currentOperation, string logPrefix)
    {
        _logger.LogInformation("[{Prefix}] 处理出库口: {LocationCode}", logPrefix, locationCode);

        var resolved = await ResolveOutboundPortLanewaysAsync(locationCode, logPrefix);
        if (resolved == null)
            return (0, 0, 0);

        var (portLocation, laneways) = resolved.Value;
        int ok = 0, fail = 0, skip = 0;

        foreach (var laneway in laneways)
        {
            try
            {
                if (await ProcessSimpleLanewayAsync(laneway, portLocation.LocationId, currentOperation, logPrefix))
                    ok++;
                else
                    skip++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Prefix}] 巷道处理失败: LanewayId={LanewayId}", logPrefix, laneway.LanewayId);
                fail++;
            }
        }

        return (ok, fail, skip);
    }

    /// <summary>
    /// 处理化成/分容出库单个巷道（共用：查重 → 查托盘 → 创建任务）
    /// </summary>
    private async Task<bool> ProcessSimpleLanewayAsync(
        Domain.Entities.Warehouse.Laneway laneway, int endLocationId,
        string currentOperation, string logPrefix)
    {
        // 检查该巷道是否已有进行中的出库任务
        var hasExistingTask = await _db.TransTasks.AsNoTracking()
            .AnyAsync(t => t.TaskType == Cst.出库
                && t.StartLocation.Rack.LanewayId == laneway.LanewayId);
        if (hasExistingTask)
        {
            _logger.LogInformation("[{Prefix}] 巷道 LanewayId={LanewayId} 已有出库任务，跳过", logPrefix, laneway.LanewayId);
            return false;
        }

        // 查找满足条件的 Unitload
        var unitload = await _db.Unitloads.AsNoTracking()
            .Include(u => u.Location).ThenInclude(l => l.Rack)
            .Include(u => u.UnitloadItems).ThenInclude(ui => ui.Material)
            .Where(u => u.Location.Rack.LanewayId == laneway.LanewayId
                && u.Location.LocationType == Location_Enum.LocationType.R.ToString()
                && u.Location.OutboundDisabled == false
                && u.Location.xExists == true
                && u.BeingMoved != true
                && u.CurrentOperation == currentOperation
                && u.UnitloadItems.Any(ui => ui.Material != null
                    && ui.Material.MaterialCode != CommonTypes.空托盘)
                && u.Location.HKPosintionState == 2
                && u.Location.HKPosintionCK == 1)
            .OrderBy(u => u.CurrentLocationTime)
            .FirstOrDefaultAsync();

        if (unitload == null)
        {
            _logger.LogInformation("[{Prefix}] 巷道 LanewayId={LanewayId} 无满足条件的托盘", logPrefix, laneway.LanewayId);
            return false;
        }

        return await CreateSingleOutboundTaskAsync(unitload, endLocationId, logPrefix);
    }

    /// <summary>
    /// 创建化成/分容出库任务（共用：事务 + 二次并发校验 + WCS 下发）
    /// </summary>
    private async Task<bool> CreateSingleOutboundTaskAsync(
        Unitload unitload, int endLocationId, string logPrefix)
    {
        var locationId = unitload.LocationId!.Value;

        await using var tx = await _db.Database.BeginTransactionAsync();

        var trackedUnitload = await _db.Unitloads.FindAsync(unitload.UnitloadId);
        if (trackedUnitload == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        // 二次并发校验
        if (trackedUnitload.BeingMoved == true || trackedUnitload.Allocated == true)
        {
            _logger.LogWarning("[{Prefix}] Unitload Id={Id} 二次检查不通过: BeingMoved={BeingMoved}, Allocated={Allocated}",
                logPrefix, unitload.UnitloadId, trackedUnitload.BeingMoved, trackedUnitload.Allocated);
            await tx.RollbackAsync();
            return false;
        }

        var trackedLocation = await _db.Locations.FindAsync(locationId);
        if (trackedLocation == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        var endLocation = await _db.Locations.FindAsync(endLocationId);
        if (endLocation == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        var transTask = new TransTask
        {
            TaskCode = await TaskCodeGenerator.GenerateAsync(_db),
            TaskType = Cst.出库,
            UnitloadId = unitload.UnitloadId,
            UnitloadCode = trackedUnitload.ContainerCode,
            StartLocationId = locationId,
            EndLocationId = endLocationId,
            ForWcs = true,
            WasSentToWcs = false,
            Ext1 = trackedUnitload.ContainerCode,
            WareHouse = endLocation.AreaName,
            LocationGroup = string.Empty
        };
        transTask.Unitload = trackedUnitload;
        transTask.StartLocation = trackedLocation;
        transTask.EndLocation = endLocation;
        _db.TransTasks.Add(transTask);

        // 标记 Unitload
        trackedUnitload.BeingMoved = true;
        trackedUnitload.Allocated = true;

        // 重置操作提示
        trackedUnitload.OpHintType = string.Empty;
        trackedUnitload.OpHintInfo = string.Empty;

        trackedLocation.OutboundCount++;
        endLocation.InboundCount++;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // WCS 下发（事务外）
        try
        {
            await _wcsBridge.SendTaskAsync(transTask);
            transTask.WasSentToWcs = true;
            transTask.SentToWcsAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _logger.LogInformation("[{Prefix}] 出库任务创建: TaskCode={TaskCode}, Unitload={Id}, Container={Container}",
                logPrefix, transTask.TaskCode, unitload.UnitloadId, trackedUnitload.ContainerCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Prefix}] WCS 下发失败: TaskCode={TaskCode}, UnitloadId={Id}",
                logPrefix, transTask.TaskCode, unitload.UnitloadId);
            return false;
        }
    }

    /// <summary>
    /// 执行高温浸润出库任务 - 双叉模式
    /// </summary>
    /// <remarks>
    /// 双叉堆垛机一次可取同 Rack + 同层 + 列±1 的两个托盘。优先查找相邻库位上满足同一 OutboundBatch 条件的配对托盘，
    /// 生成两个共享 LocationGroup 的 TransTask；无配对时降级为单叉出库。
    /// </remarks>
    public async Task<WcsResult> ExecuteGaowenDoubleOutboundAsync()
    {
        return await ExecuteDoubleOutboundCoreAsync(
            "OUTBOUNDGAOWEN",
            "PROCESSTIME_1",
            Unitload_Enum.CurrentOperation.高温浸润.ToString(),
            "高温浸润",
            "GaowenDouble",
            "OutboundTimer:GaowenDoubleOutbound");
    }

    /// <summary>
    /// 双叉出库核心流程（对标 ExecuteTimedOutboundCoreAsync）
    /// </summary>
    private async Task<WcsResult> ExecuteDoubleOutboundCoreAsync(
        string outboundDictNo, string processTimeDictNo, string cutOp,
        string bizName, string logPrefix, string lockKey)
    {
        return await ExecuteWithLockAsync(lockKey, logPrefix, async () =>
        {
            var outboundPorts = _dictService.GetItemsByNo(outboundDictNo);
            if (outboundPorts == null || outboundPorts.Count == 0)
                return ApiResultHelper.WcsFail($"未配置出库口 {outboundDictNo}", ResultCodeTypes.数据异常, -1);

            var processTime = _dictService.GetByNo(processTimeDictNo);
            if (processTime == null || !int.TryParse(processTime.Value, out int minutes))
                return ApiResultHelper.WcsFail($"未配置浸润时间 {processTimeDictNo}", ResultCodeTypes.数据异常, -1);
            var cutoffTime = DateTime.Now.AddMinutes(-minutes);

            _logger.LogInformation("[{Prefix}] 开始{BizName}双叉出库: 出库口={Count}个, 浸润时间={Minutes}分钟",
                logPrefix, bizName, outboundPorts.Count, minutes);

            foreach (var portItem in outboundPorts)
            {
                var locationCode = portItem.Value?.Trim();
                if (string.IsNullOrEmpty(locationCode)) continue;

                try
                {
                    await ProcessOutboundPortDoubleAsync(locationCode, cutoffTime, cutOp);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Prefix}] 出库口处理失败: {LocationCode}", logPrefix, locationCode);
                }
            }

            return ApiResultHelper.WcsSuccess($"{bizName}双叉出库完成", ResultCodeTypes.一, 1);
        }, bizName);
    }

    /// <summary>
    /// 处理单个出库口（双叉模式）
    /// </summary>
    private async Task ProcessOutboundPortDoubleAsync(string locationCode, DateTime cutoffTime, string cutOp)
    {
        _logger.LogInformation("[{cutOp}] 处理出库口(双叉): {LocationCode}", cutOp, locationCode);

        var resolved = await ResolveOutboundPortLanewaysAsync(locationCode, cutOp);
        if (resolved == null) return;

        var (portLocation, laneways) = resolved.Value;

        foreach (var laneway in laneways)
        {
            try
            {
                await ProcessLanewayDoubleAsync(laneway, cutoffTime, cutOp, portLocation.LocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{cutOp}] 巷道处理失败(双叉): LanewayId={LanewayId}", cutOp, laneway.LanewayId);
            }
        }
    }

    /// <summary>
    /// 处理单个巷道（双叉模式）
    /// </summary>
    private async Task ProcessLanewayDoubleAsync(
        Domain.Entities.Warehouse.Laneway laneway, DateTime cutoffTime, string cutOp, int endLocationId)
    {
        var batches = await _db.OutboundBatches.AsNoTracking()
            .Where(ob => ob.LanewayId == laneway.LanewayId
                && ob.CurrentOperation == cutOp
                && ob.Status == 1
                && ob.ErrorCount <= 3
                && ob.QuantityDelivered < ob.QuantityRequired)
            .ToListAsync();

        _logger.LogInformation("[GaowenDouble] 巷道 LanewayId={LanewayId}: 符合条件 OutboundBatches={BatchCount}个",
            laneway.LanewayId, batches.Count);

        foreach (var ob in batches)
        {
            try
            {
                await ProcessBatchDoubleAsync(ob, cutoffTime, endLocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GaowenDouble] Batch 处理异常: Id={Id}, Batch={Batch}", ob.Id, ob.Batch);
            }
        }
    }

    /// <summary>
    /// 处理单个出库批次（双叉模式）— FIFO 遍历，逐个查找相邻配对，含超发防护与防重复
    /// </summary>
    private async Task ProcessBatchDoubleAsync(
        Domain.Entities.Outbound.OutboundBatch ob, DateTime cutoffTime, int endLocationId)
    {
        int remaining = ob.QuantityRequired - ob.QuantityDelivered;

        // UnitloadItem 精确匹配（与 ProcessBatchAsync 一致）
        var query = _db.UnitloadItems.AsNoTracking()
            .Where(ui => ui.MaterialId == ob.MaterialId);

        if (!string.IsNullOrWhiteSpace(ob.Batch))
            query = query.Where(ui => ui.Batch == ob.Batch);
        if (!string.IsNullOrWhiteSpace(ob.xLevel))
            query = query.Where(ui => ui.xLevel == ob.xLevel);
        if (ob.OperationNumber.HasValue)
            query = query.Where(ui => ui.OperationNumber == ob.OperationNumber);
        if (ob.IsAdvance != 0)
            query = query.Where(ui => ui.IsAdvance == ob.IsAdvance);
        if (ob.IsSupplement != 0)
            query = query.Where(ui => ui.IsSupplement == ob.IsSupplement);

        var unitloadIds = await query
            .Select(ui => ui.UnitloadId)
            .Distinct()
            .ToListAsync();

        if (unitloadIds.Count == 0)
        {
            _logger.LogWarning("[GaowenDouble] Batch Id={Id} 未匹配到 UnitloadItem: Batch={Batch}, MaterialId={MaterialId}",
                ob.Id, ob.Batch, ob.MaterialId);
            await HandleBatchNoMatchAsync(ob);
            return;
        }

        // 筛选满足条件的 Unitload（与 ProcessBatchAsync 一致）
        var unitloads = await _db.Unitloads.AsNoTracking()
            .Where(u => unitloadIds.Contains(u.UnitloadId)
                && u.Location.Rack.LanewayId == ob.LanewayId
                && u.Location.LocationType == Location_Enum.LocationType.R.ToString()
                && u.Location.xExists == true
                && u.Location.OutboundDisabled == false
                && u.CurrentLocationTime < cutoffTime
                && u.CurrentOperation == ob.CurrentOperation
                && u.BeingMoved != true
                && u.Allocated != true
                && u.LocationId.HasValue)
            .Include(u => u.Location)
            .OrderBy(u => u.CurrentLocationTime)
            .Take(remaining)
            .ToListAsync();

        if (unitloads.Count == 0)
        {
            _logger.LogWarning("[GaowenDouble] Batch Id={Id} 有 UnitloadItem={ItemCount}个但无满足条件的 Unitload, cutoff={Cutoff}",
                ob.Id, unitloadIds.Count, cutoffTime.ToString("yyyy-MM-dd HH:mm:ss"));
            await HandleBatchNoMatchAsync(ob);
            return;
        }

        _logger.LogInformation("[GaowenDouble] Batch Id={Id}, Batch={Batch}: 匹配 Unitload={Count}个（剩余需求 {Remaining}）",
            ob.Id, ob.Batch, unitloads.Count, remaining);

        // 逐个配对遍历（防超发 + 防重复）
        var processedUnitloadIds = new HashSet<int>();
        int deliveredThisRound = 0;
        int successCount = 0;

        foreach (var unitload in unitloads)
        {
            // 防超发：达到需求量立即停止
            if (deliveredThisRound >= remaining) break;
            if (processedUnitloadIds.Contains(unitload.UnitloadId)) continue;

            try
            {
                Unitload? adjacent = null;
                // 仅当剩余需求 >= 2 时才尝试双叉（防止超发）
                if (remaining - deliveredThisRound >= 2)
                {
                    adjacent = await FindAdjacentUnitloadAsync(unitload, ob, cutoffTime, processedUnitloadIds);
                }

                if (adjacent != null)
                {
                    int delivered = await ProcessDoubleForkAsync(unitload, adjacent, ob, endLocationId);
                    deliveredThisRound += delivered;
                    processedUnitloadIds.Add(unitload.UnitloadId);
                    if (delivered == 2)
                        processedUnitloadIds.Add(adjacent.UnitloadId);
                    // delivered==1 表示主成功+相邻失败，相邻未标记可下次重试
                    successCount += delivered;
                }
                else
                {
                    // 降级单叉（复用现有方法）
                    bool ok = await ProcessLocationGroupAsync(
                        new List<Unitload> { unitload }, ob.Batch, endLocationId, ob);
                    if (ok)
                    {
                        deliveredThisRound += 1;
                        successCount += 1;
                        // 补录同库位叠盘：ProcessLocationGroupAsync 会顺带出库同库位其他 Unitload
                        var sameLocOthers = unitloads
                            .Where(u => u.LocationId == unitload.LocationId
                                && u.UnitloadId != unitload.UnitloadId
                                && !processedUnitloadIds.Contains(u.UnitloadId))
                            .Select(u => u.UnitloadId);
                        foreach (var id in sameLocOthers)
                            processedUnitloadIds.Add(id);
                    }
                    processedUnitloadIds.Add(unitload.UnitloadId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GaowenDouble] Unitload 处理异常: UnitloadId={Id}", unitload.UnitloadId);
                processedUnitloadIds.Add(unitload.UnitloadId);
            }
        }

        // 全失败场景的 ErrorCount（成功场景已在事务内更新）
        if (successCount == 0)
        {
            var trackedBatch = await _db.OutboundBatches.FindAsync(ob.Id);
            if (trackedBatch != null)
            {
                trackedBatch.ErrorCount++;
                if (trackedBatch.ErrorCount > 3)
                    trackedBatch.Status = 0;
                await _db.SaveChangesAsync();
                _logger.LogWarning("[GaowenDouble] Batch 全部失败: Id={Id}, ErrorCount={ErrorCount}",
                    ob.Id, trackedBatch.ErrorCount);
            }
        }
    }

    /// <summary>
    /// 查找相邻库位上满足同一 OutboundBatch 条件的 Unitload
    /// 相邻定义：同 RackId + 同 xLevel + xColumn 相差 1（与入库双叉 AllocateNearbyAsync 一致）
    /// </summary>
    private async Task<Unitload?> FindAdjacentUnitloadAsync(
        Unitload primaryUnitload,
        Domain.Entities.Outbound.OutboundBatch ob,
        DateTime cutoffTime,
        HashSet<int> excludeUnitloadIds)
    {
        var primaryLocation = primaryUnitload.Location;
        if (primaryLocation == null || primaryLocation.RackId == null) return null;

        var refRackId = primaryLocation.RackId.Value;
        var refLevel = primaryLocation.xLevel;
        var refColumn = primaryLocation.xColumn;

        return await _db.Unitloads.AsNoTracking()
            .Include(u => u.Location)
            .Where(u => u.UnitloadId != primaryUnitload.UnitloadId
                && !excludeUnitloadIds.Contains(u.UnitloadId)
                && u.BeingMoved != true
                && u.Allocated != true
                && u.CurrentOperation == ob.CurrentOperation
                && u.CurrentLocationTime < cutoffTime
                && u.LocationId.HasValue
                && u.Location.RackId == refRackId
                && u.Location.xLevel == refLevel
                && (u.Location.xColumn == refColumn - 1 || u.Location.xColumn == refColumn + 1)
                && u.Location.LocationType == Location_Enum.LocationType.R.ToString()
                && u.Location.xExists == true
                && u.Location.OutboundDisabled == false
                && u.UnitloadItems.Any(ui => ui.MaterialId == ob.MaterialId
                    && (ob.Batch == null || ui.Batch == ob.Batch)
                    && (ob.xLevel == null || ui.xLevel == ob.xLevel)
                    && (!ob.OperationNumber.HasValue || ui.OperationNumber == ob.OperationNumber)
                    && (ob.IsAdvance == 0 || ui.IsAdvance == ob.IsAdvance)
                    && (ob.IsSupplement == 0 || ui.IsSupplement == ob.IsSupplement)))
            .OrderBy(u => u.CurrentLocationTime)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// 双叉协调器：生成共享 LocationGroup，调用两次 CreateDoubleForkTaskAsync
    /// </summary>
    /// <returns>0=主任务也失败; 1=主成功+相邻失败; 2=两个都成功</returns>
    private async Task<int> ProcessDoubleForkAsync(
        Unitload primary, Unitload adjacent,
        Domain.Entities.Outbound.OutboundBatch ob, int endLocationId)
    {
        var locationGroup = StringExtensions.GenerateTimeStamp();

        _logger.LogInformation("[GaowenDouble] 开始双叉出库: 主={PrimaryId}, 相邻={AdjacentId}, Group={Group}, Batch={Batch}",
            primary.UnitloadId, adjacent.UnitloadId, locationGroup, ob.Batch);

        var t1 = await CreateDoubleForkTaskAsync(primary, ob, endLocationId, locationGroup, isPrimary: true);
        if (!t1.success)
        {
            _logger.LogWarning("[GaowenDouble] 主托盘任务创建失败: UnitloadId={Id}", primary.UnitloadId);
            return 0;
        }

        var t2 = await CreateDoubleForkTaskAsync(adjacent, ob, endLocationId, locationGroup, isPrimary: false);
        if (!t2.success)
        {
            _logger.LogWarning("[GaowenDouble] 相邻托盘任务创建失败: UnitloadId={Id}, 主任务已成功不回滚",
                adjacent.UnitloadId);
            return 1;
        }

        _logger.LogInformation("[GaowenDouble] 双叉出库完成: Task1={Task1Code}, Task2={Task2Code}, Group={Group}",
            t1.taskCode, t2.taskCode, locationGroup);
        return 2;
    }

    /// <summary>
    /// 双叉场景单个任务：独立事务 + WCS下发 + 失败回滚（参考 ProcessLocationGroupAsync）
    /// </summary>
    private async Task<(bool success, string? taskCode)> CreateDoubleForkTaskAsync(
        Unitload unitload,
        Domain.Entities.Outbound.OutboundBatch ob,
        int endLocationId,
        string locationGroup,
        bool isPrimary)
    {
        var locationId = unitload.LocationId!.Value;

        await using var tx = await _db.Database.BeginTransactionAsync();

        var trackedUnitload = await _db.Unitloads.FindAsync(unitload.UnitloadId);
        if (trackedUnitload == null)
        {
            await tx.RollbackAsync();
            return (false, null);
        }

        // 二次并发检查
        if (trackedUnitload.BeingMoved == true || trackedUnitload.Allocated == true)
        {
            _logger.LogWarning("[GaowenDouble] Unitload Id={Id} 二次检查不通过: BeingMoved={BeingMoved}, Allocated={Allocated}",
                unitload.UnitloadId, trackedUnitload.BeingMoved, trackedUnitload.Allocated);
            await tx.RollbackAsync();
            return (false, null);
        }

        // 同库位其他 Unitload（叠盘一起搬运）
        var additionalUnitloads = await _db.Unitloads
            .Where(u => u.LocationId == locationId
                && u.UnitloadId != unitload.UnitloadId
                && u.BeingMoved != true
                && u.Allocated != true)
            .ToListAsync();

        var allCodes = new List<string> { trackedUnitload.ContainerCode ?? "" };
        var additionalIds = new List<int>();
        foreach (var au in additionalUnitloads)
        {
            allCodes.Add(au.ContainerCode ?? "");
            additionalIds.Add(au.UnitloadId);
        }

        var trackedLocation = await _db.Locations.FindAsync(locationId);
        if (trackedLocation == null)
        {
            await tx.RollbackAsync();
            return (false, null);
        }

        var endLocation = await _db.Locations.FindAsync(endLocationId);
        if (endLocation == null)
        {
            await tx.RollbackAsync();
            return (false, null);
        }

        var transTask = new TransTask
        {
            TaskCode = await TaskCodeGenerator.GenerateAsync(_db),
            TaskType = Cst.出库,
            UnitloadId = unitload.UnitloadId,
            UnitloadCode = trackedUnitload.ContainerCode,
            StartLocationId = locationId,
            EndLocationId = endLocationId,
            ForWcs = true,
            WasSentToWcs = false,
            Ext1 = string.Join(";", allCodes),
            Ext2 = additionalIds.Count > 0 ? string.Join(";", additionalIds) : string.Empty,
            WareHouse = endLocation.AreaName,
            LocationGroup = locationGroup
        };
        transTask.Unitload = trackedUnitload;
        transTask.StartLocation = trackedLocation;
        transTask.EndLocation = endLocation;
        _db.TransTasks.Add(transTask);

        trackedUnitload.BeingMoved = true;
        trackedUnitload.Allocated = true;

        foreach (var au in additionalUnitloads)
        {
            au.BeingMoved = true;
            au.Allocated = true;
        }

        trackedLocation.OutboundCount++;
        endLocation.InboundCount++;

        var trackedBatch = await _db.OutboundBatches.FindAsync(ob.Id);
        if (trackedBatch != null)
        {
            int deliveredCount = 1 + additionalIds.Count;
            trackedBatch.QuantityDelivered += deliveredCount;
            trackedBatch.ErrorCount = 0;
            _logger.LogInformation("[GaowenDouble] Batch 出库成功(事务内): Id={Id}, 本次={Count}, 累计={Total}, IsPrimary={IsPrimary}",
                ob.Id, deliveredCount, trackedBatch.QuantityDelivered, isPrimary);
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // WCS 下发（事务外）
        try
        {
            await _wcsBridge.SendTaskAsync(transTask);
            transTask.WasSentToWcs = true;
            transTask.SentToWcsAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _logger.LogInformation("[GaowenDouble] 出库任务下发成功: TaskCode={TaskCode}, UnitloadId={Id}, Group={Group}, IsPrimary={IsPrimary}",
                transTask.TaskCode, unitload.UnitloadId, locationGroup, isPrimary);
            return (true, transTask.TaskCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GaowenDouble] WCS 下发失败，开始回滚: TaskCode={TaskCode}, UnitloadId={Id}",
                transTask.TaskCode, unitload.UnitloadId);

            try
            {
                await _wcsBridge.DeleteTaskAsync(transTask.TaskCode);
            }
            catch (Exception delEx)
            {
                _logger.LogWarning(delEx, "[GaowenDouble] 回滚时删除 wcs_tasks 失败: TaskCode={TaskCode}",
                    transTask.TaskCode);
            }

            try
            {
                await using var rollbackTx = await _db.Database.BeginTransactionAsync();

                var rbPrimary = await _db.Unitloads.FindAsync(unitload.UnitloadId);
                if (rbPrimary != null)
                {
                    rbPrimary.BeingMoved = false;
                    rbPrimary.Allocated = false;
                }

                foreach (var addId in additionalIds)
                {
                    var rbAdd = await _db.Unitloads.FindAsync(addId);
                    if (rbAdd != null)
                    {
                        rbAdd.BeingMoved = false;
                        rbAdd.Allocated = false;
                    }
                }

                var rbStartLoc = await _db.Locations.FindAsync(locationId);
                if (rbStartLoc != null && rbStartLoc.OutboundCount > 0)
                    rbStartLoc.OutboundCount--;

                var rbEndLoc = await _db.Locations.FindAsync(endLocationId);
                if (rbEndLoc != null && rbEndLoc.InboundCount > 0)
                    rbEndLoc.InboundCount--;

                var rbBatch = await _db.OutboundBatches.FindAsync(ob.Id);
                int deliveredCount = 1 + additionalIds.Count;
                if (rbBatch != null && rbBatch.QuantityDelivered >= deliveredCount)
                    rbBatch.QuantityDelivered -= deliveredCount;

                var rbTask = await _db.TransTasks.FindAsync(transTask.Id);
                if (rbTask != null)
                    _db.TransTasks.Remove(rbTask);

                await _db.SaveChangesAsync();
                await rollbackTx.CommitAsync();

                _logger.LogWarning("[GaowenDouble] WCS 下发失败已回滚: TaskCode={TaskCode}, UnitloadId={Id}",
                    transTask.TaskCode, unitload.UnitloadId);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "[GaowenDouble] 回滚也失败，数据库可能不一致，需人工检查: TaskCode={TaskCode}",
                    transTask.TaskCode);
            }

            return (false, null);
        }
    }


    #endregion
}
