using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Outbound;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Requests;
using Wms.Core.WebApi.Extensions;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 出库批次管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class OutboundBatchController : ControllerBase
{
    private readonly IRepository<OutboundBatch, int> _repository;
    private readonly ILogger<OutboundBatchController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public OutboundBatchController(
        IRepository<OutboundBatch, int> repository,
        ILogger<OutboundBatchController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取出库批次列表
    /// </summary>
    /// <param name="batch">批次（可选）</param>
    /// <param name="currentOperation">当前操作（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(int? lanewayId = null, string? batch = null, string? currentOperation = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            if (lanewayId.HasValue)
            {
                query = query.Where(m => m.LanewayId == lanewayId.Value);
            }

            if (!string.IsNullOrEmpty(batch))
            {
                query = query.Where(m => m.Batch != null && m.Batch.Contains(batch));
            }

            if (!string.IsNullOrEmpty(currentOperation))
            {
                query = query.Where(m => m.CurrentOperation == currentOperation);
            }

            var totalCount = query.Count();

            var lists = query
                .OrderBy(m => m.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResponse = new PagedResult<OutboundBatch>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<OutboundBatch>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取出库批次列表失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 根据 ID 获取出库批次
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>对象详情</returns>
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

            return Result<OutboundBatch>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取出库批次失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 创建出库批次
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <returns>创建的对象</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Result Create([FromBody] OutboundBatchRequest request)
    {
        try
        {
            var model = new OutboundBatch
            {
                LanewayId = request.LanewayId,
                MaterialId = request.MaterialId,
                CurrentOperation = request.CurrentOperation,
                OperationNumber = request.OperationNumber,
                Batch = request.Batch,
                xLevel = request.xLevel,
                QuantityRequired = request.QuantityRequired,
                QuantityDelivered = request.QuantityDelivered,
                IsAdvance = request.IsAdvance,
                IsSupplement = request.IsSupplement,
                Status = request.Status,
                Sort = request.Sort,
                ErrorCount = request.ErrorCount,
                Comment = request.Comment,
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now,
                CreatedBy = request.CreatedBy,
                ModifiedBy = request.ModifiedBy,
            };

            _repository.Add(model);

            return Result<OutboundBatch>.Success(model, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建出库批次失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新出库批次
    /// </summary>
    /// <param name="id">ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新后的对象</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Update(int id, [FromBody] OutboundBatchRequest request)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            model.LanewayId = request.LanewayId;
            model.MaterialId = request.MaterialId;
            model.CurrentOperation = request.CurrentOperation;
            model.OperationNumber = request.OperationNumber;
            model.Batch = request.Batch;
            model.xLevel = request.xLevel;
            model.QuantityRequired = request.QuantityRequired;
            model.QuantityDelivered = request.QuantityDelivered;
            model.IsAdvance = request.IsAdvance;
            model.IsSupplement = request.IsSupplement;
            model.Status = request.Status;
            model.Sort = request.Sort;
            model.ErrorCount = request.ErrorCount;
            model.Comment = request.Comment;
            model.ModifiedTime = DateTime.Now;
            model.ModifiedBy = request.ModifiedBy;

            _repository.Update(model);

            return Result<OutboundBatch>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新出库批次失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除出库批次
    /// </summary>
    /// <param name="id">出库批次 ID</param>
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
            _logger.LogError(ex, "删除出库批次失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }
}
