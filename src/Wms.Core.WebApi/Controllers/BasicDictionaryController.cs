using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Requests;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Extensions;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Common;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Filters;
using Wms.Core.WebApi.Helpers;
using Wms.Core.Application.Ports;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 基础信息管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
//[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class BasicDictionaryController : ControllerBase
{
    private readonly IRepository<BasicDictionary, int> _repository;
    private readonly ILogger<BasicDictionaryController> _logger;
    private readonly IBasicDictionaryService  _basicDictionaryService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public BasicDictionaryController(
        IRepository<BasicDictionary, int> repository,
        IBasicDictionaryService basicDictionaryService,
        ILogger<BasicDictionaryController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _basicDictionaryService = basicDictionaryService ?? throw new ArgumentNullException(nameof(basicDictionaryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取列表
    /// </summary>
    /// <param name="keyword">搜索关键字（可选）</param>
    /// <param name="parent">父级ID（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    [AllowAnonymous]
    //[ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "keyword", "inStockOnly", "type", "enabled", "pageNumber", "pageSize" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, int? parent = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            // 限制每页最大大小
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            // 关键字搜索
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.No.Contains(keyword) || m.Name.Contains(keyword));
            }

            // 按父级筛选
            if (parent.HasValue)
            {
                query = query.Where(m => m.ParentId == parent.Value);
            }

            // 获取总数
            var totalCount = query.Count();

            // 分页
            var materials = query
                .OrderBy(m => m.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResponse = new PagedResult<BasicDictionary>
            {
                Data = materials,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<BasicDictionary>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
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
            return Result<BasicDictionary>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 获取明细列表
    /// </summary>
    /// <param name="No">编码</param>
    /// <returns>明细列表</returns>
    [HttpGet("{No}/items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result GetItems(string No)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(No))
            {
                return Result.Fail("编码不能为空");
            }

            var Items = _basicDictionaryService.GetItemsByNo(No);

            var items = Items.Select(item => new
            {
                item.Id,
                item.ParentId,
                item.No,
                item.Name,
                item.Value,
                item.ExpandField1,
                item.ExpandField2,
                item.Remarks,
            }).ToList();

            return Result<object>.Success(items, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 创建对象
    /// </summary>
    /// <param name="request">创建对象请求</param>
    /// <returns>创建的对象</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Result Create([FromBody] CreateBasicDictionaryRequest request)
    {
        try
        {
            // 验证物料编码是否唯一
            if (_repository.Exists(m => m.No == request.No))
            {
                return Result.Fail("编码已存在");
            }

            var model = new BasicDictionary
            {
                ParentId = request.ParentId,
                Sort = request.Sort,
                IsNext = request.IsNext,
                No = request.No,
                Name = request.Name,
                Value = request.Value,
                ExpandField1 = request.ExpandField1,
                ExpandField2 = request.ExpandField2,
                Remarks = request.Remarks,
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now,
                CreatedBy = request.CreatedBy
            };

            var createdMaterial = _repository.Add(model);

            return Result<BasicDictionary>.Success(model, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 更新对象
    /// </summary>
    /// <param name="id">ID</param>
    /// <param name="request">更新对象请求</param>
    /// <returns>更新后的对象</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Update(int id, [FromBody] UpdateBasicDictionaryRequest request)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            // 更新字段
            model.ParentId = request.ParentId;
            model.Sort = request.Sort;
            model.IsNext = request.IsNext;
            model.Status = request.Status;
            model.No = request.No ?? model.No;
            model.Name = request.Name ?? model.Name;
            model.Value = request.Value ?? model.Value;
            model.Remarks = request.Remarks;
            model.ExpandField1 = request.ExpandField1;
            model.ExpandField2 = request.ExpandField2;
            model.ModifiedTime = DateTime.Now;
            model.ModifiedBy = request.ModifiedBy;

            _repository.Update(model);

            return Result<BasicDictionary>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 删除物料
    /// </summary>
    /// <param name="id">物料 ID</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:int}")]
    //[Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Result Delete(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            var hasChild = _repository.Find(x => x.ParentId == model.Id).FirstOrDefault();

            if (hasChild != null)
            {
                return Result.Fail("存在子级记录，无法删除");
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

    /// <summary>
    /// 启用/禁用物料
    /// </summary>
    /// <param name="id">物料 ID</param>
    /// <param name="request">启用/禁用请求</param>
    /// <returns>操作结果</returns>
    [HttpPatch("{id:int}/enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result SetEnabled(int id, [FromBody] SetEnabledRequest request)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            model.Status = request.Enabled == true ? 1 : 0;
            model.ModifiedTime = DateTime.UtcNow;
            model.ModifiedBy = request.ModifiedBy;

            _repository.Update(model);

            return Result.Success("设置成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

}
