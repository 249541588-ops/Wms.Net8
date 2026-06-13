using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Requests;
using Wms.Core.Domain.Services;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Filters;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 出货口管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class PortController : ControllerBase
{
    private readonly IRepository<Port, int> _repository;
    private readonly WmsDbContext _db;
    private readonly ILogger<PortController> _logger;
    private readonly IPortService _portService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public PortController(
        IRepository<Port, int> repository,
        WmsDbContext db,
        IPortService portService,
        ILogger<PortController> logger)
    {
        _portService = portService ?? throw new ArgumentNullException(nameof(portService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取出货口列表
    /// </summary>
    /// <param name="keyword">搜索关键字（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.PortName!.Contains(keyword) || m.PortCode!.Contains(keyword));
            }

            var totalCount = query.Count();

            var lists = query
                .OrderBy(m => m.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResponse = new PagedResult<Port>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<Port>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 根据 ID 获取出货口（含关联巷道）
    /// </summary>
    /// <param name="id">出货口 ID</param>
    /// <returns>出货口详情</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result GetById(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            return Result<Port>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取出货口关联的巷道列表
    /// </summary>
    /// <param name="id">出货口 ID</param>
    /// <returns>巷道ID列表</returns>
    [HttpGet("{id:int}/laneways")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetLanewaysByPortId(int id)
    {
        try
        {
            var lanewayIds = _db.Set<LanewayPort>()
                .Where(m => m.PortId == id)
                .Select(m => m.LanewayId)
                .ToList();

            return Result<object>.Success(lanewayIds, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取关联巷道失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 创建出货口
    /// </summary>
    /// <param name="request">创建出货口请求</param>
    /// <returns>创建的出货口</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Result Create([FromBody] CreatePortRequest request)
    {
        try
        {
            return _portService.CreatePort(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新出货口
    /// </summary>
    /// <param name="id">出货口 ID</param>
    /// <param name="request">更新出货口请求</param>
    /// <returns>更新后的出货口</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Update(int id, [FromBody] CreatePortRequest request)
    {
        try
        {
            request.Id = id;
            return _portService.CreatePort(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除出货口
    /// </summary>
    /// <param name="id">出货口 ID</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Delete(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            // 先删除关联的巷道关系
            var links = _db.Set<LanewayPort>().Where(m => m.PortId == id).ToList();
            if (links.Any())
            {
                _db.Set<LanewayPort>().RemoveRange(links);
                _db.SaveChanges();
            }

            _repository.Delete(id);
            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }
}
