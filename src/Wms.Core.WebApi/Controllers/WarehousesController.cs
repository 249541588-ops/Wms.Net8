using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Requests;
using Wms.Core.Infrastructure.Mappers;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Filters;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 仓库管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
//[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class WarehousesController : ControllerBase
{
    private readonly IRepository<Warehouse, int> _repository;
    private readonly ILogger<WarehousesController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public WarehousesController(
        IRepository<Warehouse, int> repository,
        ILogger<WarehousesController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }       

    /// <summary>
    /// 获取所有菜单
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
            // 限制每页最大大小
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            // 关键字搜索
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.UserCode.Contains(keyword) || m.xName.Contains(keyword));
            }           

            // 获取总数
            var totalCount = query.Count();

            // 分页
            var lists = query
                .OrderBy(m => m.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();


            var pagedResponse = new PagedResult<Warehouse>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<Warehouse>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
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

            return Result<Warehouse>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 创建仓库
    /// </summary>
    /// <param name="request">创建仓库请求</param>
    /// <returns>创建的仓库</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Result Create([FromBody] CreateWarehouseRequest request)
    {
        try
        {
            // 验证编码是否唯一
            if (!string.IsNullOrEmpty(request.UserCode) && _repository.Exists(m => m.UserCode == request.UserCode))
            {
                return Result.Fail("编码已存在");
            }

            var model = new Warehouse
            {
                UserCode = request.UserCode,
                xName = request.xName,
                Telephone = request.Telephone,
                AreaCode = request.AreaCode,
                Address = request.Address,
                PostCode = request.PostCode,
                Comments = request.Comments,
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now,
                CreatedBy = request.CreatedBy
            };

            _repository.Add(model);

            return Result<Warehouse>.Success(model, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新仓库
    /// </summary>
    /// <param name="id">仓库 ID</param>
    /// <param name="request">更新仓库请求</param>
    /// <returns>更新后的仓库</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Update(int id, [FromBody] UpdateWarehouseRequest request)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            if (!string.IsNullOrEmpty(request.UserCode) && request.UserCode != model.UserCode && _repository.Exists(m => m.UserCode == request.UserCode))
            {
                return Result.Fail("编码已存在");
            }

            model.UserCode = request.UserCode ?? model.UserCode;
            model.xName = request.xName ?? model.xName;
            model.Telephone = request.Telephone ?? model.Telephone;
            model.AreaCode = request.AreaCode ?? model.AreaCode;
            model.Address = request.Address ?? model.Address;
            model.PostCode = request.PostCode ?? model.PostCode;
            model.Comments = request.Comments ?? model.Comments;
            model.ModifiedTime = DateTime.Now;
            model.ModifiedBy = request.ModifiedBy;

            _repository.Update(model);

            return Result<Warehouse>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除仓库
    /// </summary>
    /// <param name="id">仓库 ID</param>
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
            return Result.Fail(ex.Message);
        }
    }


}
