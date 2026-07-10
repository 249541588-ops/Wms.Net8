using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Filters;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 货架管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class RacksController : ControllerBase
{
    private readonly IRepository<Rack, int> _repository;
    private readonly ILogger<RacksController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RacksController(
        IRepository<Rack, int> repository,
        ILogger<RacksController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取货架列表
    /// </summary>
    /// <param name="keyword">搜索关键字（可选）</param>
    /// <param name="warehouseId">仓库ID筛选（可选）</param>
    /// <param name="lanewayId">通道ID筛选（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, int? warehouseId = null, int? lanewayId = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            if (warehouseId.HasValue)
            {
                query = query.Where(m => m.WarehouseId == warehouseId.Value);
            }

            if (lanewayId.HasValue)
            {
                query = query.Where(m => m.LanewayId == lanewayId.Value);
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.RackCode!.Contains(keyword));
            }

            var totalCount = query.Count();

            var lists = query
                .OrderBy(m => m.RackId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResponse = new PagedResult<Rack>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<Rack>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 根据 ID 获取货架
    /// </summary>
    /// <param name="id">货架 ID</param>
    /// <returns>货架详情</returns>
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

            return Result<Rack>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 创建货架
    /// </summary>
    /// <param name="request">创建货架请求</param>
    /// <returns>创建的货架</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Result Create([FromBody] CreateRackRequest request)
    {
        try
        {
            if (!string.IsNullOrEmpty(request.RackCode) && _repository.Exists(m => m.RackCode == request.RackCode))
            {
                return Result.Fail("货架编码已存在");
            }

            var model = new Rack
            {
                RackCode = request.RackCode,
                WarehouseId = request.WarehouseId,
                LanewayId = request.LanewayId,
                Side = request.Side,
                Deep = request.Deep,
                Columns = request.Columns,
                Levels = request.Levels,
                Comment = request.Comment,
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now,
                CreatedBy = request.CreatedBy
            };

            _repository.Add(model);

            return Result<Rack>.Success(model, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 更新货架
    /// </summary>
    /// <param name="id">货架 ID</param>
    /// <param name="request">更新货架请求</param>
    /// <returns>更新后的货架</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Update(int id, [FromBody] UpdateRackRequest request)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            if (!string.IsNullOrEmpty(request.RackCode) && request.RackCode != model.RackCode && _repository.Exists(m => m.RackCode == request.RackCode))
            {
                return Result.Fail("货架编码已存在");
            }

            model.RackCode = request.RackCode ?? model.RackCode;
            model.WarehouseId = request.WarehouseId ?? model.WarehouseId;
            model.LanewayId = request.LanewayId ?? model.LanewayId;
            model.Side = request.Side ?? model.Side;
            model.Deep = request.Deep ?? model.Deep;
            model.Columns = request.Columns ?? model.Columns;
            model.Levels = request.Levels ?? model.Levels;
            model.Comment = request.Comment ?? model.Comment;
            model.ModifiedTime = DateTime.Now;
            model.ModifiedBy = request.ModifiedBy;

            _repository.Update(model);

            return Result<Rack>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 删除货架
    /// </summary>
    /// <param name="id">货架 ID</param>
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

            _repository.Delete(id);
            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }
}
