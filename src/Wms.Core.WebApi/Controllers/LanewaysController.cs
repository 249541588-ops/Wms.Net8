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
/// 巷道管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
//[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class LanewaysController : ControllerBase
{
    private readonly IRepository<Laneway, int> _repository;
    private readonly ILogger<LanewaysController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LanewaysController(
        IRepository<Laneway, int> repository,
        ILogger<LanewaysController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }       

    /// <summary>
    /// 获取巷道列表
    /// </summary>
    /// <param name="keyword">搜索关键字（可选）</param>
    /// <param name="warehouseId">仓库ID筛选（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, int? warehouseId = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            // 限制每页最大大小
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            // 按仓库筛选
            if (warehouseId.HasValue)
            {
                query = query.Where(m => m.Warehouse.Id == warehouseId.Value);
            }

            // 关键字搜索
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.LanewayCode!.Contains(keyword) || (m.Area != null && m.Area.Contains(keyword)));
            }           

            // 获取总数
            var totalCount = query.Count();

            // 分页
            var lists = query
                .OrderBy(m => m.LanewayId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();


            var pagedResponse = new PagedResult<Laneway>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<Laneway>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 获取所有巷道（不分页，用于下拉选择）
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAllList()
    {
        try
        {
            var list = _repository.GetAll()
                .OrderBy(m => m.LanewayId)
                .Select(m => new { m.LanewayId, m.LanewayCode, m.Area })
                .ToList();
            return Result<object>.Success(list, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取巷道列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 根据 ID 获取对象
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>对象详情</returns>
    [HttpGet("{id:int}")]
    //[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
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

            return Result<Laneway>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 创建巷道
    /// </summary>
    /// <param name="request">创建巷道请求</param>
    /// <returns>创建的巷道</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Result Create([FromBody] CreateLanewayRequest request)
    {
        try
        {
            // 验证编码是否唯一
            if (!string.IsNullOrEmpty(request.LanewayCode) && _repository.Exists(m => m.LanewayCode == request.LanewayCode))
            {
                return Result.Fail("通道编码已存在");
            }

            var model = new Laneway
            {
                LanewayCode = request.LanewayCode,
                WarehouseId = request.WarehouseId,
                Area = request.Area,
                Comment = request.Comment,
                Automated = request.Automated,
                DoubleDeep = request.DoubleDeep,
                ReservedLocationCount = request.ReservedLocationCount,
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now,
                CreatedBy = request.CreatedBy
            };

            _repository.Add(model);

            return Result<Laneway>.Success(model, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 更新巷道
    /// </summary>
    /// <param name="id">巷道 ID</param>
    /// <param name="request">更新巷道请求</param>
    /// <returns>更新后的巷道</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Update(int id, [FromBody] UpdateLanewayRequest request)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            if (!string.IsNullOrEmpty(request.LanewayCode) && request.LanewayCode != model.LanewayCode && _repository.Exists(m => m.LanewayCode == request.LanewayCode))
            {
                return Result.Fail("通道编码已存在");
            }

            model.LanewayCode = request.LanewayCode ?? model.LanewayCode;
            model.WarehouseId = request.WarehouseId ?? model.WarehouseId;
            model.Area = request.Area ?? model.Area;
            model.Comment = request.Comment ?? model.Comment;
            model.Automated = request.Automated ?? model.Automated;
            model.Offline = request.Offline ?? model.Offline;
            model.OfflineComment = request.OfflineComment ?? model.OfflineComment;
            model.DoubleDeep = request.DoubleDeep ?? model.DoubleDeep;
            model.ReservedLocationCount = request.ReservedLocationCount ?? model.ReservedLocationCount;
            model.ModifiedTime = DateTime.Now;
            model.ModifiedBy = request.ModifiedBy;

            _repository.Update(model);

            return Result<Laneway>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 删除巷道
    /// </summary>
    /// <param name="id">巷道 ID</param>
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
