using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Archive;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Requests;
using Wms.Core.Application.Ports;
using Wms.Core.Infrastructure.Mappers;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Filters;

namespace Wms.Core.WebApi.Controllers.Unitloads;

/// <summary>
/// 归档货载管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
//[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class ArchivedUnitloadsController : ControllerBase
{
    private readonly IRepository<ArchivedUnitload, int> _repository;
    private readonly IUnitloadService _unitloadService;
    private readonly WmsDbContext _db;
    private readonly ILogger<ArchivedUnitloadsController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ArchivedUnitloadsController(
        IRepository<ArchivedUnitload, int> repository,
        IUnitloadService unitloadService,
        WmsDbContext db,
        ILogger<ArchivedUnitloadsController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitloadService = unitloadService ?? throw new ArgumentNullException(nameof(unitloadService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }       

    /// <summary>
    /// 获取归档货载列表
    /// </summary>
    /// <param name="containerCode">容器编码（可选）</param>
    /// <param name="batch">批次（可选）</param>
    /// <param name="currentOperation">当前工艺（可选）</param>
    /// <param name="operationNumber">工艺次数（可选）</param>
    /// <param name="barCode">电芯码（可选，模糊匹配）</param>
    /// <param name="hasCountingError">是否异常（可选）</param>
    /// <param name="locationCode">当前位置（可选，模糊匹配）</param>
    /// <param name="warehouseId">所属库区 ID（可选）</param>
    /// <param name="materialCode">物料编码（可选）</param>
    /// <param name="archivedAtStart">归档时间-开始（可选）</param>
    /// <param name="archivedAtEnd">归档时间-结束（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(
        string? containerCode = null,
        string? batch = null,
        string? currentOperation = null,
        int? operationNumber = null,
        string? barCode = null,
        bool? hasCountingError = null,
        string? locationCode = null,
        int? warehouseId = null,
        string? materialCode = null,
        DateTime? archivedAtStart = null,
        DateTime? archivedAtEnd = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            if (!string.IsNullOrEmpty(containerCode))
            {
                query = query.Where(m => m.ContainerCode != null && m.ContainerCode.Contains(containerCode));
            }

            if (!string.IsNullOrEmpty(currentOperation))
            {
                query = query.Where(m => m.CurrentOperation == currentOperation);
            }

            if (operationNumber.HasValue)
            {
                query = query.Where(m => m.OperationNumber == operationNumber.Value);
            }

            // 批次在 ArchivedUnitloadItem 上，需要通过子查询过滤
            if (!string.IsNullOrEmpty(batch))
            {
                var matchedIds = _db.Set<ArchivedUnitloadItem>()
                    .Where(i => i.Batch == batch)
                    .Select(i => i.UnitloadId)
                    .Distinct()
                    .ToList();
                query = query.Where(m => matchedIds.Contains(m.Id));
            }

            // 电芯码：ArchivedUnitloadItemDetail → ArchivedUnitloadItem → ArchivedUnitload（归档实体无导航属性，两步查询）
            if (!string.IsNullOrEmpty(barCode))
            {
                var matchedItemIds = _db.Set<ArchivedUnitloadItemDetail>()
                    .Where(d => d.BarCode != null && d.BarCode.Contains(barCode) && d.UnitloadItemId.HasValue)
                    .Select(d => d.UnitloadItemId.Value)
                    .Distinct()
                    .ToList();
                var matchedIds = _db.Set<ArchivedUnitloadItem>()
                    .Where(i => matchedItemIds.Contains(i.Id))
                    .Select(i => i.UnitloadId)
                    .Distinct()
                    .ToList();
                query = query.Where(m => matchedIds.Contains(m.Id));
            }

            // 物料编码：ArchivedUnitloadItem 与 Materials 手动 Join（沿用控制器内现有 join 风格）
            if (!string.IsNullOrEmpty(materialCode))
            {
                var matchedIds = (from i in _db.Set<ArchivedUnitloadItem>()
                                  join m in _db.Set<Materials>() on i.MaterialId equals m.MaterialId
                                  where m.MaterialCode == materialCode
                                  select i.UnitloadId)
                                  .Distinct()
                                  .ToList();
                query = query.Where(m => matchedIds.Contains(m.Id));
            }

            // 是否异常：直接字段过滤
            if (hasCountingError.HasValue)
            {
                query = query.Where(m => m.HasCountingError == hasCountingError.Value);
            }

            // 当前位置：通过 LocationId 子查询（ArchivedUnitload 只有 LocationId，无导航）
            if (!string.IsNullOrEmpty(locationCode))
            {
                var matchedLocationIds = _db.Set<Location>()
                    .Where(l => l.LocationCode != null && l.LocationCode.Contains(locationCode))
                    .Select(l => l.LocationId)
                    .ToList();
                query = query.Where(m => m.LocationId.HasValue && matchedLocationIds.Contains(m.LocationId.Value));
            }

            // 所属库区：通过 Location.WarehouseId 子查询
            if (warehouseId.HasValue)
            {
                var matchedLocationIds = _db.Set<Location>()
                    .Where(l => l.WarehouseId == warehouseId.Value)
                    .Select(l => l.LocationId)
                    .ToList();
                query = query.Where(m => m.LocationId.HasValue && matchedLocationIds.Contains(m.LocationId.Value));
            }

            // 归档时间范围（ArchivedAt 是非可空 DateTime）
            if (archivedAtStart.HasValue)
            {
                query = query.Where(m => m.ArchivedAt >= archivedAtStart.Value);
            }
            if (archivedAtEnd.HasValue)
            {
                query = query.Where(m => m.ArchivedAt <= archivedAtEnd.Value);
            }

            var totalCount = query.Count();

            // 分页查询归档货载，左连接 Location 获取库位编码
            var lists = (from au in query
                         join loc in _db.Set<Location>() on au.LocationId equals loc.LocationId into locs
                         from loc in locs.DefaultIfEmpty()
                         orderby au.ArchivedAt descending
                         select new
                         {
                             au.Id,
                             au.ContainerCode,
                             au.CurrentOperation,
                             au.NextOperation,
                             au.OperationNumber,
                             au.IsAdvance,
                             au.CurrentLocationTime,
                             au.OpHintType,
                             au.OpHintInfo,
                             au.HasMsgError,
                             au.HasCountingError,
                             au.CreatedBy,
                             au.CreatedTime,
                             au.ArchivedAt,
                             au.ArchiveReason,
                             LocationCode = loc != null ? loc.LocationCode : null,
                         })
                         .Skip((pageNumber - 1) * pageSize)
                         .Take(pageSize)
                         .ToList();

            // 获取物料汇总信息（批次、数量、物料编码/名称）
            var ids = lists.Select(x => x.Id).ToList();
            var itemSummaries = _db.Set<ArchivedUnitloadItem>()
                .Where(i => ids.Contains(i.UnitloadId))
                .GroupBy(i => i.UnitloadId)
                .Select(g => new
                {
                    UnitloadId = g.Key,
                    g.FirstOrDefault()!.Batch,
                    BatteryCount = (int?)g.Sum(i => i.Quantity),
                    g.FirstOrDefault()!.MaterialId,
                })
                .ToList();

            var matIds = itemSummaries.Select(s => s.MaterialId).Distinct().ToList();
            var mats = _db.Set<Materials>()
                .Where(m => matIds.Contains(m.MaterialId))
                .Select(m => new { m.MaterialId, m.MaterialCode, m.Description })
                .ToDictionary(m => m.MaterialId);

            var result = lists.Select(x =>
            {
                var summary = itemSummaries.FirstOrDefault(s => s.UnitloadId == x.Id);
                return new
                {
                    x.Id,
                    x.ContainerCode,
                    x.CurrentOperation,
                    x.NextOperation,
                    x.OperationNumber,
                    x.IsAdvance,
                    x.CurrentLocationTime,
                    x.OpHintType,
                    x.OpHintInfo,
                    x.HasMsgError,
                    x.HasCountingError,
                    x.CreatedBy,
                    x.CreatedTime,
                    x.ArchivedAt,
                    x.ArchiveReason,
                    x.LocationCode,
                    summary?.Batch,
                    summary?.BatteryCount,
                    MaterialCode = summary != null && mats.ContainsKey(summary.MaterialId) ? mats[summary.MaterialId].MaterialCode : null,
                    MaterialName = summary != null && mats.ContainsKey(summary.MaterialId) ? (mats[summary.MaterialId].Description + "【"+ mats[summary.MaterialId].MaterialCode + "】" ) : null,
                };
            }).ToList();

            var pagedResponse = new PagedResult<object>
            {
                Data = result,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<object>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取归档列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 根据 ID 获取归档对象
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
            var model = _db.Set<ArchivedUnitload>().FirstOrDefault(m => m.Id == id);

            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            // 获取物料明细
            var items = _db.Set<ArchivedUnitloadItem>()
                .Where(i => i.UnitloadId == id)
                .ToList();

            var itemIds = items.Select(i => i.Id).ToList();

            // 获取电芯明细
            var details = _db.Set<ArchivedUnitloadItemDetail>()
                .Where(d => itemIds.Contains(d.UnitloadItemId!.Value))
                .ToList();

            // 获取物料信息
            var materialIds = items.Select(i => i.MaterialId).Distinct().ToList();
            var materials = _db.Set<Materials>()
                .Where(m => materialIds.Contains(m.MaterialId))
                .ToDictionary(m => m.MaterialId);

            // 获取库位编码
            string? locationCode = null;
            if (model.LocationId.HasValue)
            {
                locationCode = _db.Set<Location>()
                    .Where(l => l.LocationId == model.LocationId.Value)
                    .Select(l => l.LocationCode)
                    .FirstOrDefault();
            }

            return Result<object>.Success(new
            {
                Unitload = model,
                LocationCode = locationCode,
                Items = items.Select(i => new
                {
                    i.Id,
                    i.UnitloadId,
                    i.MaterialId,
                    i.Batch,
                    i.StockStatus,
                    i.Quantity,
                    i.FalseQuantity,
                    i.Uom,
                    i.ProductionTime,
                    i.OutOrdering,
                    i.BoxCode,
                    i.Position,
                    i.xLevel,
                    i.OperationNumber,
                    i.BatchNumber,
                    i.IsAdvance,
                    i.IsSupplement,
                    MaterialCode = materials.ContainsKey(i.MaterialId) ? materials[i.MaterialId].MaterialCode : null,
                    MaterialName = materials.ContainsKey(i.MaterialId) ? materials[i.MaterialId].Description : null,
                    Details = details.Where(d => d.UnitloadItemId == i.Id).ToList()
                })
            }, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取归档对象失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }        

    /// <summary>
    /// 删除归档记录
    /// </summary>
    /// <param name="id">归档记录 ID</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Delete(int id)
    {
        try
        {
            var model = _db.Set<ArchivedUnitload>().FirstOrDefault(m => m.Id == id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            // 获取关联的物料明细 ID
            var itemIds = _db.Set<ArchivedUnitloadItem>()
                .Where(i => i.UnitloadId == id)
                .Select(i => i.Id)
                .ToList();

            // 删除电芯明细
            if (itemIds.Any())
            {
                var detailsToRemove = _db.Set<ArchivedUnitloadItemDetail>()
                    .Where(d => itemIds.Contains(d.UnitloadItemId!.Value));
                _db.Set<ArchivedUnitloadItemDetail>().RemoveRange(detailsToRemove);
            }

            // 删除物料明细
            var itemsToRemove = _db.Set<ArchivedUnitloadItem>()
                .Where(i => i.UnitloadId == id);
            _db.Set<ArchivedUnitloadItem>().RemoveRange(itemsToRemove);

            // 删除归档货载
            _db.Set<ArchivedUnitload>().Remove(model);
            _db.SaveChanges();

            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除归档记录失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 还原
    /// </summary>
    /// <param name="id">归档记录 ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("{id:int}/recover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Recover(int id, string? modifiedBy = null)
    {
        return _unitloadService.Recover(id, modifiedBy);
    }


}
