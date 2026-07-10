using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Requests;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 废料批次设置 API 控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class WasteBatchSettingController : ControllerBase
{
    private readonly IRepository<WasteBatchSetting, int> _repository;
    private readonly ILogger<WasteBatchSettingController> _logger;

    public WasteBatchSettingController(
        IRepository<WasteBatchSetting, int> repository,
        ILogger<WasteBatchSettingController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取废料批次设置列表
    /// </summary>
    [HttpGet]
    public Result GetAll(string? keyword = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(m => m.LocationCode.Contains(keyword) || m.ContainerCode!.Contains(keyword) || m.Batch.Contains(keyword));

            var totalCount = query.Count();
            var items = query
                .OrderBy(m => m.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResponse = new PagedResult<WasteBatchSetting>
            {
                Data = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<WasteBatchSetting>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取废料批次设置列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 根据 ID 获取废料批次设置
    /// </summary>
    [HttpGet("{id:int}")]
    public Result GetById(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
                return Result.Fail("记录不存在", "404");
            return Result<WasteBatchSetting>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取废料批次设置失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 创建废料批次设置
    /// </summary>
    [HttpPost]
    public Result Create([FromBody] CreateWasteBatchSettingRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LocationCode))
                return Result.Fail("库位编码不能为空");
            if (string.IsNullOrWhiteSpace(request.Batch))
                return Result.Fail("批次号不能为空");

            var model = new WasteBatchSetting
            {
                IsBuiltIn = request.IsBuiltIn,
                LocationType = request.LocationType,
                LocationCode = request.LocationCode,
                ContainerCode = request.ContainerCode,
                Batch = request.Batch,
                CreatedBy = User.FindFirst("username")?.Value
            };

            _repository.Add(model);
            return Result<WasteBatchSetting>.Success(model, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建废料批次设置失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 更新废料批次设置
    /// </summary>
    [HttpPut("{id:int}")]
    public Result Update(int id, [FromBody] UpdateWasteBatchSettingRequest request)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
                return Result.Fail("记录不存在", "404");

            if (request.IsBuiltIn.HasValue) model.IsBuiltIn = request.IsBuiltIn.Value;
            if (request.LocationType.HasValue) model.LocationType = request.LocationType.Value;
            if (request.LocationCode != null) model.LocationCode = request.LocationCode;
            if (request.ContainerCode != null) model.ContainerCode = request.ContainerCode;
            if (request.Batch != null) model.Batch = request.Batch;
            model.ModifiedBy = User.FindFirst("username")?.Value;

            _repository.Update(model);
            return Result<WasteBatchSetting>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新废料批次设置失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 删除废料批次设置
    /// </summary>
    [HttpDelete("{id:int}")]
    public Result Delete(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
                return Result.Fail("记录不存在", "404");

            _repository.Delete(id);
            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除废料批次设置失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }
}
