using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Requests;
using Wms.Core.Application.Ports;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 电芯 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class BatteryCellsController : ControllerBase
{
    private readonly IBatteryCellService _service;
    private readonly ILogger<BatteryCellsController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public BatteryCellsController(
        IBatteryCellService service,
        ILogger<BatteryCellsController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <param name="keyword">关键字（可选）</param>
    /// <param name="batch">批次（可选）</param>
    /// <param name="xLevel">档位/X等级（可选）</param>
    /// <param name="containerCode">容器编码（可选）</param>
    /// <param name="status">状态（可选）</param>
    /// <param name="materialId">物料ID（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    [HttpGet]
    public Result GetAll(string? keyword = null, string? batch = null, string? xLevel = null, string? containerCode = null, string? status = null, int? materialId = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            var (data, totalCount) = _service.GetPagedList(keyword, batch, xLevel, containerCode, status, materialId, pageNumber, pageSize);
            var pagedResult = new PagedResult<BatteryCell>
            {
                Data = data,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
            return Result<PagedResult<BatteryCell>>.Success(pagedResult, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取电芯列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 获取全部列表（下拉选择用）
    /// </summary>
    [HttpGet("all")]
    public Result GetAllList()
    {
        try
        {
            var list = _service.GetPagedList(null, null, null, null, null, null, 1, 1000);
            var data = list.Data.Select(s => new { s.Id, s.BarCode, s.Batch });
            return Result<object>.Success(data.ToList(), "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取电芯下拉列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 根据ID获取
    /// </summary>
    [HttpGet("{id:int}")]
    public Result GetById(int id)
    {
        try
        {
            var entity = _service.GetById(id);
            if (entity == null)
                return Result.Fail("记录不存在", "404");

            return Result<BatteryCell>.Success(entity, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取电芯详情失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 创建
    /// </summary>
    [HttpPost]
    public Result Create([FromBody] BatteryCellRequest request)
    {
        try
        {
            var entity = _service.Create(request);
            return Result<BatteryCell>.Success(entity, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建电芯失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 更新
    /// </summary>
    [HttpPut("{id:int}")]
    public Result Update(int id, [FromBody] BatteryCellRequest request)
    {
        try
        {
            var entity = _service.Update(id, request);
            if (entity == null)
                return Result.Fail("记录不存在", "404");

            return Result<BatteryCell>.Success(entity, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新电芯失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 删除
    /// </summary>
    [HttpDelete("{id:int}")]
    public Result Delete(int id)
    {
        try
        {
            var deleted = _service.Delete(id);
            if (!deleted)
                return Result.Fail("记录不存在", "404");

            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除电芯失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }
}
