using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Repositories;
using Wms.Core.WebApi.Extensions;

namespace Wms.Core.WebApi.Controllers.Unitloads;

/// <summary>
/// 货载操作日志 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class UnitloadsOpsController : ControllerBase
{
    private readonly IRepository<UnitloadOp, int> _repository;
    private readonly ILogger<UnitloadsOpsController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UnitloadsOpsController(
        IRepository<UnitloadOp, int> repository,
        ILogger<UnitloadsOpsController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取操作日志列表
    /// </summary>
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
                query = query.Where(m => m.ContainerCode.Contains(keyword)
                    || (m.Comment != null && m.Comment.Contains(keyword))
                    || (m.CreatedBy != null && m.CreatedBy.Contains(keyword)));
            }

            var totalCount = query.Count();

            var lists = query
                .OrderByDescending(m => m.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.OpType,
                    m.Direction,
                    m.ContainerCode,
                    m.Comment,
                    m.CreatedBy,
                    m.CreatedTime,
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
            _logger.LogError(ex, "获取操作日志列表失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }
}
