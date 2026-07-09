using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Filters;
using Wms.Core.WebApi.Models;

namespace Wms.Core.WebApi.Controllers.Api;

/// <summary>
/// 杭可 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
[InternalIpWhitelist]
public partial class HangkeController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<HangkeController> _logger;

    /// <summary>
    /// 按货位编码分组的异步锁（细粒度串行化，防丢失更新与重复取盘竞态）
    /// </summary>
    /// <remarks>
    /// 替代原 <c>lock("UpdateLocationStatus")</c> 字符串字面量锁，避免字符串拘留导致的死锁风险，
    /// 同时支持 async/await。
    /// </remarks>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locationLocks = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public HangkeController(
        IServiceScopeFactory scopeFactory,
        IBackgroundTaskQueue taskQueue,
        ILogger<HangkeController> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _taskQueue = taskQueue ?? throw new ArgumentNullException(nameof(taskQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 1. 货位状态变更
    /// </summary>
    /// <param name="vm">货位状态变更请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>WcsResult 表示处理结果</returns>
    /// <remarks>
    /// HKState 取值：
    /// 1、可入库 2、可出库 3、异常维护 4、作业中 5、温度报警 6、烟雾报警 7、作业完成 8、移库
    /// </remarks>
    [HttpPost("UpdateLocationStatus")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> UpdateLocationStatus(HangKeStatus vm, CancellationToken ct = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string? locationCode = vm?.LocationCode;
        int hkState = vm?.HKState ?? 0;
        WcsResult result;

        // 业务级校验：货位编码与状态合法性
        if (vm == null || string.IsNullOrWhiteSpace(vm.LocationCode))
        {
            stopwatch.Stop();
            result = ApiResultHelper.WcsFail("货位不能空", ResultCodeTypes.数据异常, -1);
            _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(BuildInterfaceLog(
                locationCode, hkState, success: false, result.msg, stopwatch.ElapsedMilliseconds)));
            return result;
        }

        if (vm.HKState < 1 || vm.HKState > 8)
        {
            stopwatch.Stop();
            result = ApiResultHelper.WcsFail($"状态 {vm.HKState} 非法，应为 1-8", ResultCodeTypes.数据异常, -1);
            _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(BuildInterfaceLog(
                locationCode, hkState, success: false, result.msg, stopwatch.ElapsedMilliseconds)));
            return result;
        }

        _logger.LogInformation(
            "[Hangke.UpdateLocationStatus] 收到请求 Location={LocationCode} HKState={HKState}",
            vm.LocationCode, vm.HKState);

        // 按货位编码串行化：防止同一货位的并发更新丢失（Bug #9）与 HKPosintionCK 竞态（Bug #13）
        SemaphoreSlim? sem = null;
        try
        {
            sem = _locationLocks.GetOrAdd(vm.LocationCode, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                result = await ExecuteStatusUpdateAsync(vm, stopwatch, ct).ConfigureAwait(false);
            }
            finally
            {
                // 仅在当前无人等待时清理，避免字典无限增长；微小竞态由 GetOrAdd 兜底新建实例
                if (sem.CurrentCount > 0 && _locationLocks.TryRemove(vm.LocationCode, out var removed))
                {
                    removed.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "[Hangke.UpdateLocationStatus] 处理失败 Location={LocationCode} HKState={HKState} 耗时={Ms}ms",
                vm.LocationCode, vm.HKState, stopwatch.ElapsedMilliseconds);

            // 对外仅返回通用消息，避免泄露表名/字段名/SQL 片段（Bug #2）
            result = ApiResultHelper.WcsFail(
                $"处理失败，请联系管理员（货位：{vm.LocationCode}）",
                ResultCodeTypes.程序异常, -1);

            _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(BuildInterfaceLog(
                vm.LocationCode, vm.HKState, success: false, "内部错误", stopwatch.ElapsedMilliseconds)));
        }
        finally
        {
            sem?.Release();
        }

        return result;
    }

    /// <summary>
    /// 执行货位状态变更的核心业务逻辑（在持有按货位锁的前提下调用）
    /// </summary>
    private async Task<WcsResult> ExecuteStatusUpdateAsync(HangKeStatus vm, Stopwatch stopwatch, CancellationToken ct)
    {
        // 业务事务：EF Core 显式事务，SaveChanges 与 Commit 分离，确保日志记录与实际状态一致（Bug #12）
        // 使用独立 scope 直接从数据库查询货位，绕过 ILocationService 的 IMemoryCache，确保拿到最新数据
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();

        Location? loc = await db.Locations
            .FirstOrDefaultAsync(l => l.LocationCode == vm.LocationCode, ct)
            .ConfigureAwait(false);

        if (loc == null)
        {
            stopwatch.Stop();
            var failResult = ApiResultHelper.WcsFail(
                $"{vm.LocationCode} 货位不存在", ResultCodeTypes.数据异常, -1);
            _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(BuildInterfaceLog(
                vm.LocationCode, vm.HKState, success: false, failResult.msg, stopwatch.ElapsedMilliseconds)));
            return failResult;
        }

        // 作业完成（7）：先检查货位上是否有 Unitload，若空记 Warning 但仍更新（业务决策）
        if (vm.HKState == (int)Interface_Enum.HKLocationStatus.作业完成)
        {
            await WarnIfNoUnitloadAsync(db, loc, ct).ConfigureAwait(false);
        }

        // 状态映射（保留原 switch 全部分支语义）
        var stateName = EnumHelper.GetEnumName<Interface_Enum.HKLocationStatus>(vm.HKState);
        ApplyStatusToLocation(loc, vm.HKState, stateName);

        using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        db.Entry(loc).Property(l => l.ModifiedTime).CurrentValue = DateTime.UtcNow;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        stopwatch.Stop();
        var result = ApiResultHelper.WcsSuccess(
            $"{vm.LocationCode} 状态 {vm.HKState} 设置成功",
            ResultCodeTypes.一, 1);

        _logger.LogInformation(
            "[Hangke.UpdateLocationStatus] 完成 Location={LocationCode} HKState={HKState} 耗时={Ms}ms",
            vm.LocationCode, vm.HKState, stopwatch.ElapsedMilliseconds);

        _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(BuildInterfaceLog(
            vm.LocationCode, vm.HKState, success: true, result.msg, stopwatch.ElapsedMilliseconds)));

        return result;
    }

    /// <summary>
    /// 将 HKState 映射到 Location 的禁入/禁出/CK 字段（保留原 switch 语义）
    /// </summary>
    private static void ApplyStatusToLocation(Location loc, int hkState, string stateName)
    {
        switch (hkState)
        {
            case (int)Interface_Enum.HKLocationStatus.可入库:
                loc.InboundDisabled = false;
                loc.InboundDisabledComment = "杭可通知可入";
                break;

            case (int)Interface_Enum.HKLocationStatus.可出库:
            case (int)Interface_Enum.HKLocationStatus.移库:
                loc.OutboundDisabled = false;
                loc.OutboundDisabledComment = "杭可通知可出";
                break;

            case (int)Interface_Enum.HKLocationStatus.异常维护:
            case (int)Interface_Enum.HKLocationStatus.作业中:
            case (int)Interface_Enum.HKLocationStatus.温度警报:
            case (int)Interface_Enum.HKLocationStatus.烟雾报警:
                loc.InboundDisabled = true;
                loc.OutboundDisabled = true;
                loc.InboundDisabledComment = Truncate($"杭可通知{stateName}，禁入", 255);
                loc.OutboundDisabledComment = Truncate($"杭可通知{stateName}，禁出", 255);
                break;

            case (int)Interface_Enum.HKLocationStatus.作业完成:
                loc.HKPosintionCK = 0;
                loc.InboundDisabled = true;
                loc.OutboundDisabled = true;
                loc.InboundDisabledComment = Truncate($"杭可通知{stateName}，禁入", 255);
                loc.OutboundDisabledComment = Truncate($"杭可通知{stateName}，禁出", 255);
                break;

            default:
                loc.InboundDisabled = true;
                loc.OutboundDisabled = true;
                loc.InboundDisabledComment = Truncate($"杭可通知{stateName}，禁入", 255);
                loc.OutboundDisabledComment = Truncate($"杭可通知{stateName}，禁出", 255);
                break;
        }

        loc.HKPosintionState = hkState;
    }

    /// <summary>
    /// 检查货位是否存在 Unitload；不存在则记 Warning（仅作业完成分支调用）
    /// </summary>
    private async Task WarnIfNoUnitloadAsync(WmsDbContext db, Location loc, CancellationToken ct)
    {
        try
        {
            var hasUnitload = await db.Unitloads
                .AsNoTracking()
                .AnyAsync(u => u.LocationId == loc.LocationId, ct)
                .ConfigureAwait(false);

            if (!hasUnitload)
            {
                _logger.LogWarning(
                    "[Hangke.UpdateLocationStatus] 货位 {LocCode} 收到作业完成(7)但无 Unitload，下游 PickPendingPalletAsync 将自动跳过",
                    loc.LocationCode);
            }
        }
        catch (Exception ex)
        {
            // 检查不应阻塞业务流程，仅记错误
            _logger.LogError(ex,
                "[Hangke.UpdateLocationStatus] 检查 Unitload 时发生异常 Location={LocCode}", loc.LocationCode);
        }
    }

    /// <summary>
    /// 异步写入接口日志（失败仅记录日志，不影响业务流程）
    /// </summary>
    private async Task SaveInterfaceLogAsync(InterfaceLog log)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var logDb = scope.ServiceProvider.GetRequiredService<WmsLogDbContext>();
            logDb.InterfaceLogs.Add(log);
            await logDb.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Hangke] 写入接口日志失败: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 构造接口日志
    /// </summary>
    private static InterfaceLog BuildInterfaceLog(
        string? locationCode, int hkState, bool success, string? comment, long durationMs)
    {
        var safeComment = comment ?? string.Empty;
        return new InterfaceLog
        {
            Source = "Hangke",
            Endpoint = "UpdateLocationStatus",
            Requester = "Hangke-Device",
            LocationCode = locationCode,
            RequestBody = $"{{\"LocationCode\":\"{locationCode}\",\"HKState\":{hkState}}}",
            ResponseBody = $"{{\"success\":{success.ToString().ToLowerInvariant()},\"comment\":\"{safeComment}\"}}",
            Success = success,
            DurationMs = durationMs,
            Comment = Truncate(safeComment, 2000),
            CreatedTime = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// 截断字符串到指定长度（防止 [MaxLength] 字段溢出）
    /// </summary>
    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    /// <summary>
    /// 2. 物流货架编码更新
    /// </summary>
    /// <param name="rackCode">物流货架号</param>
    /// <param name="newRackCode">杭可货架号（≤7 字符，否则超出 AnotherCode[16] 限制）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>WcsResult.currentoperation 为受影响货位数</returns>
    [HttpPost("UpdateLocationRackCode")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<WcsResult> UpdateLocationRackCode(
        string rackCode, string newRackCode, CancellationToken ct = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        // 输入校验
        if (string.IsNullOrWhiteSpace(rackCode))
        {
            stopwatch.Stop();
            var fail = ApiResultHelper.WcsFail("物流货架号不能空", ResultCodeTypes.数据异常, -1);
            _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(BuildInterfaceLogForRack(
                rackCode, newRackCode, success: false, fail.msg, stopwatch.ElapsedMilliseconds)));
            return fail;
        }

        if (string.IsNullOrWhiteSpace(newRackCode))
        {
            stopwatch.Stop();
            var fail = ApiResultHelper.WcsFail("杭可货架号不能空", ResultCodeTypes.数据异常, -1);
            _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(BuildInterfaceLogForRack(
                rackCode, newRackCode, success: false, fail.msg, stopwatch.ElapsedMilliseconds)));
            return fail;
        }

        // 防御 AnotherCode[MaxLength(16)] 溢出：
        // 后缀 "-{cc}-{ll}" 在 cc>=10/ll<=9 时最长 8 字符（如 "-12-09"），故 newRackCode ≤ 7
        if (newRackCode.Length > 7)
        {
            stopwatch.Stop();
            var fail = ApiResultHelper.WcsFail(
                $"杭可货架号 {newRackCode} 过长（≤7 字符，否则超出 AnotherCode[16] 限制）",
                ResultCodeTypes.数据异常, -1);
            _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(BuildInterfaceLogForRack(
                rackCode, newRackCode, success: false, fail.msg, stopwatch.ElapsedMilliseconds)));
            return fail;
        }

        _logger.LogInformation(
            "[Hangke.UpdateLocationRackCode] 收到请求 Rack={RackCode} NewRack={NewRackCode}",
            rackCode, newRackCode);

        // 按 "RACK:" 前缀键串行化，避免与 LocationCode 键冲突；防止并发覆盖
        var lockKey = "RACK:" + rackCode;
        SemaphoreSlim? sem = null;
        WcsResult result;
        try
        {
            sem = _locationLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                result = await ExecuteRackCodeUpdateAsync(rackCode, newRackCode, stopwatch, ct)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (sem.CurrentCount > 0 && _locationLocks.TryRemove(lockKey, out var removed))
                {
                    removed.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "[Hangke.UpdateLocationRackCode] 处理失败 Rack={RackCode} NewRack={NewRackCode} 耗时={Ms}ms",
                rackCode, newRackCode, stopwatch.ElapsedMilliseconds);

            // 对外仅返回通用消息，避免泄露 SQL/表名（Bug G）
            result = ApiResultHelper.WcsFail(
                $"处理失败，请联系管理员（物流货架号：{rackCode}）",
                ResultCodeTypes.程序异常, -1);

            _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(BuildInterfaceLogForRack(
                rackCode, newRackCode, success: false, "内部错误", stopwatch.ElapsedMilliseconds)));
        }
        finally
        {
            sem?.Release();
        }

        return result;
    }

    /// <summary>
    /// 执行物流货架号 → 杭可货架号映射的核心逻辑（在持有按 rack 锁的前提下调用）
    /// </summary>
    private async Task<WcsResult> ExecuteRackCodeUpdateAsync(
        string rackCode, string newRackCode, Stopwatch stopwatch, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        // 查询匹配货位（EF Core 自动 INNER JOIN Racks）
        var locations = await db.Locations
            .Where(l => l.Rack != null && l.Rack.RackCode == rackCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // 计算并赋值 AnotherCode（保留原 xColumn>=10 / <10 双分支语义）
        foreach (var loc in locations)
        {
            var colPart = loc.xColumn >= 10 ? loc.xColumn.ToString() : "0" + loc.xColumn;
            var lvlPart = "0" + loc.xLevel;
            loc.AnotherCode = Truncate($"{newRackCode}-{colPart}-{lvlPart}", 16);
            loc.ModifiedTime = DateTime.UtcNow;
        }

        var affected = await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        stopwatch.Stop();
        var result = ApiResultHelper.WcsSuccess(
            $"物流货架号 {rackCode} → 杭可货架号 {newRackCode} 更新 {affected} 个货位",
            ResultCodeTypes.一, affected);

        _logger.LogInformation(
            "[Hangke.UpdateLocationRackCode] 完成 Rack={RackCode} NewRack={NewRackCode} 更新 {Affected} 个货位 耗时={Ms}ms",
            rackCode, newRackCode, affected, stopwatch.ElapsedMilliseconds);

        _ = _taskQueue.QueueAsync(_ => SaveInterfaceLogAsync(BuildInterfaceLogForRack(
            rackCode, newRackCode, success: true, result.msg, stopwatch.ElapsedMilliseconds, affected)));

        return result;
    }

    /// <summary>
    /// 构造 UpdateLocationRackCode 接口日志
    /// </summary>
    private static InterfaceLog BuildInterfaceLogForRack(
        string? rackCode, string? newRackCode, bool success, string? comment, long durationMs, int affected = 0)
    {
        var safeComment = comment ?? string.Empty;
        return new InterfaceLog
        {
            Source = "Hangke",
            Endpoint = "UpdateLocationRackCode",
            Requester = "Hangke-Device",
            LocationCode = Truncate($"{rackCode}->{newRackCode}", 50),
            ContainerCode = affected > 0 ? $"affected={affected}" : null,
            RequestBody = $"{{\"rackCode\":\"{rackCode}\",\"newRackCode\":\"{newRackCode}\"}}",
            ResponseBody = $"{{\"success\":{success.ToString().ToLowerInvariant()},\"comment\":\"{safeComment}\"}}",
            Success = success,
            DurationMs = durationMs,
            Comment = Truncate(safeComment, 2000),
            CreatedTime = DateTime.UtcNow,
        };
    }
}

