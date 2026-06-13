using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Archive;
using Wms.Core.Domain.Repositories;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Controllers.Tasks;

/// <summary>
/// 历史任务管理 API 控制器（只读）
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class ArchivedTasksController : ControllerBase
{
    private readonly IRepository<ArchivedTask, int> _repository;
    private readonly WmsDbContext _db;
    private readonly ILogger<ArchivedTasksController> _logger;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="db"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public ArchivedTasksController(
        IRepository<ArchivedTask, int> repository,
        WmsDbContext db,
        ILogger<ArchivedTasksController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取历史任务列表
    /// </summary>
    /// <param name="keyword">搜索关键字（搜 TaskCode）</param>
    /// <param name="taskType">任务类型筛选（入库/出库/移库）</param>
    /// <param name="cancelled">是否取消筛选</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, string? taskType = null, bool? cancelled = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _db.Set<ArchivedTask>().AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(t => t.TaskCode!.Contains(keyword));
            }

            if (!string.IsNullOrEmpty(taskType))
            {
                query = query.Where(t => t.TaskType == taskType);
            }

            if (cancelled.HasValue)
            {
                query = query.Where(t => t.Cancelled == cancelled.Value);
            }

            var totalCount = query.Count();

            var lists = query
                .OrderByDescending(t => t.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.TaskCode,
                    t.TaskType,
                    t.UnitloadCode,
                    t.FromLocationCode,
                    t.ToLocationCode,
                    t.ActualLocationCode,
                    t.ForWcs,
                    t.WasSentToWcs,
                    t.SentToWcsAt,
                    t.OrderCode,
                    t.WareHouse,
                    t.LocationGroup,
                    t.Status,
                    t.Cancelled,
                    t.Comment,
                    t.CreatedTime,
                    t.ArchivedAt
                })
                .ToList();

            var pagedResponse = new PagedResult<object>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<object>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取历史任务列表失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取历史任务详情
    /// </summary>
    /// <param name="id">历史任务 ID</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result GetById(int id)
    {
        try
        {
            var task = _db.Set<ArchivedTask>().Find(id);
            if (task == null)
            {
                return Result.Fail("历史任务不存在", "404");
            }

            return Result<ArchivedTask>.Success(task, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取历史任务详情失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }
}
