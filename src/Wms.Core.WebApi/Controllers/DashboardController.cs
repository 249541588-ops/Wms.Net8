using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 首页 Dashboard 统计 API（运营概览仪表盘）
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly WmsDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DashboardController> _logger;

    // 订单终态列表（排除法：状态不在此列表中视为"待处理"）
    // 实现前需确认数据库实际 Status 取值，必要时调整
    private static readonly string[] OrderFinishedStatuses =
        { "已完成", "已关闭", "已取消", "completed", "cancelled", "Cancelled" };

    // 缓存键
    private const string SummaryCacheKey = "dashboard:summary";
    private const string Trend7CacheKey = "dashboard:trend:7";
    private const string Trend30CacheKey = "dashboard:trend:30";

    // 缓存过期
    private static readonly TimeSpan SummaryCacheExpiration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Trend7CacheExpiration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Trend30CacheExpiration = TimeSpan.FromMinutes(30);

    // 缓存击穿防护锁
    private static readonly SemaphoreSlim SummaryLock = new(1, 1);
    private static readonly SemaphoreSlim Trend7Lock = new(1, 1);
    private static readonly SemaphoreSlim Trend30Lock = new(1, 1);

    public DashboardController(
        WmsDbContext db,
        IMemoryCache cache,
        ILogger<DashboardController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取首页聚合数据（6 个统计卡片 + 3 个列表面板）
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Result> GetSummary()
    {
        // 命中缓存直接返回
        if (_cache.TryGetValue(SummaryCacheKey, out DashboardSummaryDto? cached) && cached != null)
        {
            return Result<DashboardSummaryDto>.Success(cached, "获取成功");
        }

        try
        {
            // 缓存击穿防护：同一时间只允许一个请求查 DB
            await SummaryLock.WaitAsync();
            try
            {
                // double-check
                if (_cache.TryGetValue(SummaryCacheKey, out cached) && cached != null)
                {
                    return Result<DashboardSummaryDto>.Success(cached, "获取成功");
                }

                // 卡片统计（核心）：失败则整体失败
                var stats = await GetDashboardStatsAsync();

                // 列表（增强）：每个独立 try-catch，失败返回空列表
                var lowStockAlerts = await SafeListAsync(() => GetLowStockAlertsAsync(10));
                var recentInbound = await SafeListAsync(() => GetRecentOrdersAsync(isInbound: true, top: 5));
                var recentOutbound = await SafeListAsync(() => GetRecentOrdersAsync(isInbound: false, top: 5));
                var pendingTasks = await SafeListAsync(() => GetPendingTasksAsync(10));

                var summary = new DashboardSummaryDto
                {
                    Stats = stats,
                    LowStockAlerts = lowStockAlerts,
                    RecentInboundOrders = recentInbound,
                    RecentOutboundOrders = recentOutbound,
                    PendingTasks = pendingTasks,
                    GeneratedAt = DateTime.Now
                };

                _cache.Set(SummaryCacheKey, summary, SummaryCacheExpiration);

                return Result<DashboardSummaryDto>.Success(summary, "获取成功");
            }
            finally
            {
                SummaryLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Dashboard 汇总数据失败: {Message}", ex.Message);
            return Result.Fail("获取首页数据失败");
        }
    }

    /// <summary>
    /// 获取入库/出库趋势（按天分组）
    /// </summary>
    /// <param name="days">天数（默认 7，可选 30）</param>
    [HttpGet("trend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Result> GetTrend([FromQuery] int days = 7)
    {
        // 仅允许 7 或 30
        days = days == 30 ? 30 : 7;

        var cacheKey = days == 7 ? Trend7CacheKey : Trend30CacheKey;
        var cacheExpiration = days == 7 ? Trend7CacheExpiration : Trend30CacheExpiration;
        var semaphore = days == 7 ? Trend7Lock : Trend30Lock;

        if (_cache.TryGetValue(cacheKey, out TrendResultDto? cached) && cached != null)
        {
            return Result<TrendResultDto>.Success(cached, "获取成功");
        }

        try
        {
            await semaphore.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cached) && cached != null)
                {
                    return Result<TrendResultDto>.Success(cached, "获取成功");
                }

                // 日期范围（含今天，off-by-one 修正）
                var today = DateTime.Now.Date;
                var startDate = today.AddDays(-(days - 1));
                var endDate = today.AddDays(1); // 开区间，包含今天全天

                // 原生 SQL（参考 InOutStatisticsProvider），参数化防注入，NOLOCK 容忍脏读
                var sql = @"
                    SELECT CONVERT(date, f.CreatedTime) AS D,
                           CASE WHEN f.BizType IN ('入库','入库双叉') THEN 'IN' ELSE 'OUT' END AS Dir,
                           SUM(f.Quantity) AS Qty,
                           COUNT(DISTINCT f.ContainerCode) AS Trays
                    FROM Flows f WITH (NOLOCK)
                    WHERE f.CreatedTime >= @start AND f.CreatedTime < @end
                      AND f.BizType IN ('入库','入库双叉','出库')
                    GROUP BY CONVERT(date, f.CreatedTime),
                             CASE WHEN f.BizType IN ('入库','入库双叉') THEN 'IN' ELSE 'OUT' END";

                var rawRows = await _db.Database.SqlQueryRaw<TrendRow>(sql,
                    new SqlParameter("@start", startDate),
                    new SqlParameter("@end", endDate)).ToListAsync();

                // 填充完整日期序列（包括无数据的日期）
                var dateList = Enumerable.Range(0, days).Select(i => startDate.AddDays(i)).ToList();
                var points = dateList.Select(d =>
                {
                    var dateOnly = d.Date;
                    var inbound = rawRows.FirstOrDefault(r => r.D == dateOnly && r.Dir == "IN");
                    var outbound = rawRows.FirstOrDefault(r => r.D == dateOnly && r.Dir == "OUT");
                    return new TrendPointDto
                    {
                        Date = d.ToString("yyyy-MM-dd"),
                        InboundQuantity = inbound?.Qty ?? 0,
                        OutboundQuantity = outbound?.Qty ?? 0,
                        InboundTrayCount = inbound?.Trays ?? 0,
                        OutboundTrayCount = outbound?.Trays ?? 0
                    };
                }).ToList();

                var result = new TrendResultDto
                {
                    Days = days,
                    Points = points,
                    GeneratedAt = DateTime.Now
                };

                _cache.Set(cacheKey, result, cacheExpiration);

                return Result<TrendResultDto>.Success(result, "获取成功");
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取趋势数据失败: {Message}", ex.Message);
            return Result.Fail("获取趋势数据失败");
        }
    }

    /// <summary>
    /// 获取库存告警列表（分页，独立页面用）
    /// </summary>
    [HttpGet("alerts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Result> GetAlerts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            // 先聚合库存总量
            var stockSums = await _db.Stocks.AsNoTracking()
                .GroupBy(s => s.MaterialId)
                .Select(g => new { MaterialId = g.Key, Total = g.Sum(s => s.Quantity ?? 0) })
                .ToDictionaryAsync(x => x.MaterialId, x => x.Total);

            var candidates = await _db.Materials.AsNoTracking()
                .Where(m => m.Enabled == true && m.LowerBound > 0)
                .Select(m => new
                {
                    m.MaterialId,
                    m.MaterialCode,
                    m.Description,
                    m.LowerBound
                })
                .ToListAsync();

            var allAlerts = candidates
                .Select(m =>
                {
                    var current = stockSums.TryGetValue(m.MaterialId, out var s) ? s : 0m;
                    var lower = m.LowerBound ?? 0m;
                    return new AlertItemDto
                    {
                        MaterialId = m.MaterialId,
                        MaterialCode = m.MaterialCode,
                        Description = m.Description,
                        CurrentStock = current,
                        LowerBound = m.LowerBound,
                        Shortage = lower - current,
                        AlertLevel = lower > 0 && current < lower * 0.5m ? "critical" : "warning"
                    };
                })
                .Where(a => a.CurrentStock < (a.LowerBound ?? 0))
                .OrderBy(a => a.CurrentStock / (a.LowerBound ?? 1))
                .ToList();

            var totalCount = allAlerts.Count;
            var paged = allAlerts
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResult = new PagedResult<AlertItemDto>
            {
                Data = paged,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<AlertItemDto>>.Success(pagedResult, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取库存告警列表失败: {Message}", ex.Message);
            return Result.Fail("获取告警列表失败");
        }
    }

    #region 私有查询方法

    /// <summary>
    /// 查询 6 个统计卡片数据（核心，失败抛出由上层捕获）
    /// </summary>
    private async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        // 卡片 1：总库存数（记录数 + 数量总和）
        var stockQuery = _db.Stocks.AsNoTracking();
        var totalStockCount = await stockQuery.CountAsync();
        var totalStockQuantity = await stockQuery.SumAsync(s => s.Quantity ?? 0m);

        // 卡片 2/3：待入库 / 待出库（未关闭 且 状态非终态）
        var pendingInbound = await _db.InboundOrders.AsNoTracking()
            .Where(o => o.Closed != true)
            .Where(o => o.Status == null || !OrderFinishedStatuses.Contains(o.Status))
            .CountAsync();

        var pendingOutbound = await _db.OutboundOrders.AsNoTracking()
            .Where(o => o.Closed != true)
            .Where(o => o.Status == null || !OrderFinishedStatuses.Contains(o.Status))
            .CountAsync();

        // 卡片 4：待执行任务（未发送 WCS）
        var pendingTaskCount = await _db.TransTasks.AsNoTracking()
            .Where(t => t.WasSentToWcs != true)
            .CountAsync();

        // 卡片 5：库位利用率（COUNT，不加载全表）
        var totalLocations = await _db.Locations.AsNoTracking()
            .Where(l => l.xExists)
            .CountAsync();
        var occupiedLocations = await _db.Locations.AsNoTracking()
            .Where(l => l.xExists && (l.InboundCount > 0 || l.UnitloadCount > 0))
            .CountAsync();
        var utilizationRate = totalLocations > 0
            ? Math.Round((decimal)occupiedLocations / totalLocations * 100, 2)
            : 0m;

        // 卡片 6：库存告警物料种类数（先聚合再过滤）
        var stockSums = await _db.Stocks.AsNoTracking()
            .GroupBy(s => s.MaterialId)
            .Select(g => new { MaterialId = g.Key, Total = g.Sum(s => s.Quantity ?? 0) })
            .ToDictionaryAsync(x => x.MaterialId, x => x.Total);

        var alertCandidates = await _db.Materials.AsNoTracking()
            .Where(m => m.Enabled == true && m.LowerBound > 0)
            .Select(m => new { m.MaterialId, m.LowerBound })
            .ToListAsync();

        var stockAlertCount = alertCandidates
            .Count(m => (stockSums.TryGetValue(m.MaterialId, out var s) ? s : 0m) < (m.LowerBound ?? 0));

        return new DashboardStatsDto
        {
            TotalStockCount = totalStockCount,
            TotalStockQuantity = Math.Round(totalStockQuantity, 2),
            PendingInboundCount = pendingInbound,
            PendingOutboundCount = pendingOutbound,
            PendingTaskCount = pendingTaskCount,
            TotalLocationCount = totalLocations,
            OccupiedLocationCount = occupiedLocations,
            LocationUtilizationRate = utilizationRate,
            StockAlertCount = stockAlertCount
        };
    }

    /// <summary>
    /// 查询低库存告警列表（Top N）
    /// </summary>
    private async Task<List<AlertItemDto>> GetLowStockAlertsAsync(int top)
    {
        var stockSums = await _db.Stocks.AsNoTracking()
            .GroupBy(s => s.MaterialId)
            .Select(g => new { MaterialId = g.Key, Total = g.Sum(s => s.Quantity ?? 0) })
            .ToDictionaryAsync(x => x.MaterialId, x => x.Total);

        var candidates = await _db.Materials.AsNoTracking()
            .Where(m => m.Enabled == true && m.LowerBound > 0)
            .Select(m => new
            {
                m.MaterialId,
                m.MaterialCode,
                m.Description,
                m.LowerBound
            })
            .ToListAsync();

        return candidates
            .Select(m =>
            {
                var current = stockSums.TryGetValue(m.MaterialId, out var s) ? s : 0m;
                var lower = m.LowerBound ?? 0m;
                return new AlertItemDto
                {
                    MaterialId = m.MaterialId,
                    MaterialCode = m.MaterialCode,
                    Description = m.Description,
                    CurrentStock = current,
                    LowerBound = m.LowerBound,
                    Shortage = lower - current,
                    AlertLevel = lower > 0 && current < lower * 0.5m ? "critical" : "warning"
                };
            })
            .Where(a => a.CurrentStock < (a.LowerBound ?? 0))
            .OrderBy(a => a.CurrentStock / (a.LowerBound ?? 1))
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// 查询最近入库/出库单
    /// </summary>
    private async Task<List<RecentOrderDto>> GetRecentOrdersAsync(bool isInbound, int top)
    {
        if (isInbound)
        {
            return await _db.InboundOrders.AsNoTracking()
                .OrderByDescending(o => o.CreatedTime)
                .Take(top)
                .Select(o => new RecentOrderDto
                {
                    Id = o.Id,
                    OrderCode = o.InboundOrderCode,
                    BizType = o.BizType,
                    BizOrder = o.BizOrder,
                    Status = o.Status,
                    Closed = o.Closed,
                    CreatedTime = o.CreatedTime,
                    CreatedBy = o.CreatedBy
                })
                .ToListAsync();
        }

        return await _db.OutboundOrders.AsNoTracking()
            .OrderByDescending(o => o.CreatedTime)
            .Take(top)
            .Select(o => new RecentOrderDto
            {
                Id = o.Id,
                OrderCode = o.OutboundOrderCode,
                BizType = o.BizType,
                BizOrder = o.BizOrder,
                Status = o.Status,
                Closed = o.Closed,
                CreatedTime = o.CreatedTime,
                CreatedBy = o.CreatedBy
            })
            .ToListAsync();
    }

    /// <summary>
    /// 查询待办任务（Top N，未发送 WCS）
    /// </summary>
    private async Task<List<PendingTaskDto>> GetPendingTasksAsync(int top)
    {
        return await _db.TransTasks.AsNoTracking()
            .Include(t => t.Unitload)
            .Include(t => t.StartLocation)
            .Include(t => t.EndLocation)
            .Where(t => t.WasSentToWcs != true)
            .OrderByDescending(t => t.CreatedTime)
            .Take(top)
            .Select(t => new PendingTaskDto
            {
                Id = t.Id,
                TaskCode = t.TaskCode,
                TaskType = t.TaskType,
                ContainerCode = t.Unitload != null ? t.Unitload.ContainerCode : null,
                StartLocationCode = t.StartLocation != null ? t.StartLocation.LocationCode : null,
                EndLocationCode = t.EndLocation != null ? t.EndLocation.LocationCode : null,
                WasSentToWcs = t.WasSentToWcs,
                OrderCode = t.OrderCode,
                CreatedTime = t.CreatedTime
            })
            .ToListAsync();
    }

    /// <summary>
    /// 安全执行列表查询（失败返回空列表，不拖垮整体 summary）
    /// </summary>
    private async Task<List<T>> SafeListAsync<T>(Func<Task<List<T>>> factory)
    {
        try
        {
            return await factory();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard 子查询失败，降级为空列表: {Message}", ex.Message);
            return new List<T>();
        }
    }

    #endregion
}
