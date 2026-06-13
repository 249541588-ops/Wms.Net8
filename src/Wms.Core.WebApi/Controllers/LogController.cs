using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 日志管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class LogController : ControllerBase
{
    private readonly WmsDbContext _db;
    private readonly WmsLogDbContext _logDb;
    private readonly ILogger<LogController> _logger;

    public LogController(WmsDbContext db, WmsLogDbContext logDb, ILogger<LogController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logDb = logDb ?? throw new ArgumentNullException(nameof(logDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region SystemLogs（主库）

    /// <summary>
    /// 分页查询系统操作日志
    /// </summary>
    [HttpGet("SystemLogs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetSystemLogs(
        string? keyword = null,
        string? module = null,
        string? method = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _db.SystemLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    (x.Module != null && x.Module.Contains(keyword)) ||
                    (x.Action != null && x.Action.Contains(keyword)) ||
                    (x.Url != null && x.Url.Contains(keyword)) ||
                    (x.UserName != null && x.UserName.Contains(keyword)) ||
                    (x.RequestBody != null && x.RequestBody.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(module))
                query = query.Where(x => x.Module == module);

            if (!string.IsNullOrWhiteSpace(method))
                query = query.Where(x => x.HttpMethod == method);

            if (startTime.HasValue)
                query = query.Where(x => x.OperationTime >= startTime.Value);

            if (endTime.HasValue)
                query = query.Where(x => x.OperationTime <= endTime.Value);

            var totalCount = query.Count();

            var list = query
                .OrderByDescending(x => x.OperationTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.OperationTime,
                    x.HttpMethod,
                    x.Module,
                    x.Action,
                    x.Url,
                    x.StatusCode,
                    x.DurationMs,
                    x.RequestBody,
                    x.IpAddress,
                    x.UserName,
                    x.UserId,
                    x.Success
                })
                .ToList();

            var pagedResponse = new PagedResult<object>
            {
                Data = list,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<object>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询系统日志失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 清理系统操作日志（按天数）
    /// </summary>
    [HttpDelete("SystemLogs/Cleanup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result CleanupSystemLogs(int days = 30)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = _db.Database.ExecuteSqlInterpolated($"DELETE FROM SystemLogs WHERE OperationTime < {cutoff}");
            return Result<object>.Success(new { deleted }, $"已清理 {deleted} 条记录");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理系统日志失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除单条系统日志
    /// </summary>
    [HttpDelete("SystemLogs/{id:int}")]
    public Result DeleteSystemLog(int id)
    {
        try
        {
            var log = _db.SystemLogs.FirstOrDefault(x => x.Id == id);
            if (log == null)
                return Result.Fail("记录不存在", "404");

            _db.SystemLogs.Remove(log);
            _db.SaveChanges();
            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除系统日志失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取系统日志清理前的记录数预览
    /// </summary>
    [HttpGet("SystemLogs/Cleanup/Preview")]
    public Result PreviewSystemLogsCleanup(int days = 30)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var count = _db.SystemLogs.Count(x => x.OperationTime < cutoff);
            return Result<object>.Success(new { count, days }, $"将清理 {count} 条 {days} 天前的记录");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预览系统日志清理失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    #endregion

    #region InterfaceLogs（独立日志库 WmsLogsDb）

    /// <summary>
    /// 分页查询接口通信日志
    /// </summary>
    [HttpGet("InterfaceLogs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetInterfaceLogs(
        string? source = null,
        string? endpoint = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        bool? success = null,
        bool? isDuplicate = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _logDb.InterfaceLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(source))
                query = query.Where(x => x.Source == source);

            if (!string.IsNullOrWhiteSpace(endpoint))
                query = query.Where(x => x.Endpoint == endpoint);

            if (startTime.HasValue)
                query = query.Where(x => x.CreatedTime >= startTime.Value);

            if (endTime.HasValue)
                query = query.Where(x => x.CreatedTime <= endTime.Value);

            if (success.HasValue)
                query = query.Where(x => x.Success == success.Value);

            if (isDuplicate.HasValue)
                query = query.Where(x => x.IsDuplicate == isDuplicate.Value);

            var totalCount = query.Count();

            var list = query
                .OrderByDescending(x => x.CreatedTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.CreatedTime,
                    x.Source,
                    x.Endpoint,
                    x.Requester,
                    x.LocationCode,
                    x.ContainerCode,
                    x.RequestBody,
                    x.ResponseBody,
                    x.Success,
                    x.DurationMs,
                    x.IsDuplicate,
                    x.Comment
                })
                .ToList();

            var pagedResponse = new PagedResult<object>
            {
                Data = list,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<object>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询接口日志失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 清理接口通信日志（按天数）
    /// </summary>
    [HttpDelete("InterfaceLogs/Cleanup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result CleanupInterfaceLogs(int days = 60)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var deleted = _logDb.Database.ExecuteSqlInterpolated($"DELETE FROM InterfaceLogs WHERE CreatedTime < {cutoff}");
            return Result<object>.Success(new { deleted }, $"已清理 {deleted} 条记录");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理接口日志失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取接口日志清理前的记录数预览
    /// </summary>
    [HttpGet("InterfaceLogs/Cleanup/Preview")]
    public Result PreviewInterfaceLogsCleanup(int days = 60)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var count = _logDb.InterfaceLogs.Count(x => x.CreatedTime < cutoff);
            return Result<object>.Success(new { count, days }, $"将清理 {count} 条 {days} 天前的记录");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预览接口日志清理失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    #endregion

    #region LocationOps（主库）

    /// <summary>
    /// 分页查询库位操作记录
    /// </summary>
    [HttpGet("LocationOps")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetLocationOps(
        int? locationId = null,
        string? opType = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _db.LocationOps.AsQueryable();

            if (locationId.HasValue)
                query = query.Where(x => x.LocationId == locationId.Value);

            if (!string.IsNullOrWhiteSpace(opType))
                query = query.Where(x => x.OpType == opType);

            if (startTime.HasValue)
                query = query.Where(x => x.CreatedTime >= startTime.Value);

            if (endTime.HasValue)
                query = query.Where(x => x.CreatedTime <= endTime.Value);

            var totalCount = query.Count();

            var list = query
                .OrderByDescending(x => x.CreatedTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.LocationId,
                    x.OpType,
                    x.Url,
                    x.Comment,
                    x.PreviousState,
                    x.NewState,
                    x.CreatedTime,
                    x.CreatedBy
                })
                .ToList();

            var pagedResponse = new PagedResult<object>
            {
                Data = list,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<object>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询库位操作记录失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    #endregion
}
