using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Text.Json;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Filters;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 库位管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class LocationsController : ControllerBase
{
    private readonly IRepository<Location, int> _repository;
    private readonly WmsDbContext _db;
    private readonly ILogger<LocationsController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LocationsController(
        IRepository<Location, int> repository,
        WmsDbContext db,
        ILogger<LocationsController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取库位列表
    /// </summary>
    /// <param name="keyword">搜索关键字（可选）</param>
    /// <param name="warehouseId">仓库ID筛选（可选）</param>
    /// <param name="rackId">货架ID筛选（可选）</param>
    /// <param name="locationType">货架类型</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, int? warehouseId = null, int? rackId = null, string? locationType = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            if (warehouseId.HasValue)
            {
                query = query.Where(m => m.WarehouseId == warehouseId.Value);
            }

            if (rackId.HasValue)
            {
                query = query.Where(m => m.RackId == rackId.Value);
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.LocationCode!.Contains(keyword) || m.RequestType!.Contains(keyword));
            }

            if (!string.IsNullOrEmpty(locationType))
            {
                query = query.Where(m => m.LocationType!.Contains(locationType));
            }

            var totalCount = query.Count();

            var lists = query
                .OrderBy(m => m.LocationId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResponse = new PagedResult<Location>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<Location>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 根据 ID 获取库位
    /// </summary>
    /// <param name="id">库位 ID</param>
    /// <returns>库位详情</returns>
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

            return Result<Location>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 创建库位
    /// </summary>
    /// <param name="request">创建库位请求</param>
    /// <returns>创建的库位</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Result Create([FromBody] CreateLocationRequest request)
    {
        try
        {
            if (!string.IsNullOrEmpty(request.LocationCode) && _repository.Exists(m => m.LocationCode == request.LocationCode))
            {
                return Result.Fail("库位编码已存在");
            }

            var model = new Location
            {
                LocationCode = request.LocationCode,
                LocationType = request.LocationType,
                RackId = request.RackId,
                WarehouseId = request.WarehouseId,
                xColumn = request.xColumn ?? 0,
                xLevel = request.xLevel ?? 0,
                InboundLimit = request.InboundLimit ?? 0,
                OutboundLimit = request.OutboundLimit ?? 0,
                WeightLimit = request.WeightLimit ?? 0,
                HeightLimit = request.HeightLimit ?? 0,
                StorageGroup = request.StorageGroup,
                SubStorageGroup = request.SubStorageGroup,
                RequestType = request.RequestType,
                Tag = request.Tag,
                AreaName = request.AreaName,
                LanewayCodes = request.LanewayCodes,
                xSpecification = request.xSpecification,
                Comment = request.Comment,
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now,
                CreatedBy = request.CreatedBy
            };

            _repository.Add(model);

            // 记录 LocationOp
            var createOp = new LocationOp
            {
                LocationId = model.LocationId,
                OpType = "Create",
                Url = HttpContext.Request.Path,
                CreatedTime = DateTime.UtcNow,
                CreatedBy = HttpContext.User.FindFirst("username")?.Value,
                NewState = JsonSerializer.Serialize(new { model.LocationCode, model.LocationType, model.RackId, model.WarehouseId, model.RequestType }),
                Comment = $"创建库位 {model.LocationCode}"
            };
            _db.LocationOps.Add(createOp);

            return Result<Location>.Success(model, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新库位
    /// </summary>
    /// <param name="id">库位 ID</param>
    /// <param name="request">更新库位请求</param>
    /// <returns>更新后的库位</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Update(int id, [FromBody] UpdateLocationRequest request)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            if (!string.IsNullOrEmpty(request.LocationCode) && request.LocationCode != model.LocationCode && _repository.Exists(m => m.LocationCode == request.LocationCode))
            {
                return Result.Fail("库位编码已存在");
            }

            model.LocationCode = request.LocationCode ?? model.LocationCode;
            model.LocationType = request.LocationType ?? model.LocationType;
            model.RackId = request.RackId ?? model.RackId;
            model.WarehouseId = request.WarehouseId ?? model.WarehouseId;
            model.xColumn = request.xColumn ?? model.xColumn;
            model.xLevel = request.xLevel ?? model.xLevel;
            model.InboundLimit = request.InboundLimit ?? model.InboundLimit;
            model.OutboundLimit = request.OutboundLimit ?? model.OutboundLimit;
            model.InboundDisabled = request.InboundDisabled ?? model.InboundDisabled;
            model.InboundDisabledComment = request.InboundDisabledComment ?? model.InboundDisabledComment;
            model.OutboundDisabled = request.OutboundDisabled ?? model.OutboundDisabled;
            model.OutboundDisabledComment = request.OutboundDisabledComment ?? model.OutboundDisabledComment;
            model.WeightLimit = request.WeightLimit ?? model.WeightLimit;
            model.HeightLimit = request.HeightLimit ?? model.HeightLimit;
            model.StorageGroup = request.StorageGroup ?? model.StorageGroup;
            model.SubStorageGroup = request.SubStorageGroup ?? model.SubStorageGroup;
            model.RequestType = request.RequestType ?? model.RequestType;
            model.Tag = request.Tag ?? model.Tag;
            model.AreaName = request.AreaName ?? model.AreaName;
            model.LanewayCodes = request.LanewayCodes ?? model.LanewayCodes;
            model.xSpecification = request.xSpecification ?? model.xSpecification;
            model.Comment = request.Comment ?? model.Comment;
            model.ModifiedTime = DateTime.Now;
            model.ModifiedBy = request.ModifiedBy;

            // 记录 LocationOp（仅当状态相关字段有变化时）
            var stateFields = new
            {
                model.LocationCode, model.LocationType, model.InboundDisabled,
                model.OutboundDisabled, model.xExists, model.StorageGroup, model.SubStorageGroup,
                model.RequestType, model.InboundLimit, model.OutboundLimit
            };
            var updateOp = new LocationOp
            {
                LocationId = model.LocationId,
                OpType = "Update",
                Url = HttpContext.Request.Path,
                CreatedTime = DateTime.UtcNow,
                CreatedBy = HttpContext.User.FindFirst("username")?.Value,
                PreviousState = JsonSerializer.Serialize(stateFields),
                NewState = JsonSerializer.Serialize(new
                {
                    request.LocationCode, request.LocationType, request.InboundDisabled,
                    request.OutboundDisabled, request.StorageGroup, request.SubStorageGroup,
                    request.RequestType, request.InboundLimit, request.OutboundLimit
                }),
                Comment = $"更新库位 {model.LocationCode}"
            };
            _db.LocationOps.Add(updateOp);

            _repository.Update(model);

            return Result<Location>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除库位
    /// </summary>
    /// <param name="id">库位 ID</param>
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

            // 记录 LocationOp
            var deleteOp = new LocationOp
            {
                LocationId = model.LocationId,
                OpType = "Delete",
                Url = HttpContext.Request.Path,
                CreatedTime = DateTime.UtcNow,
                CreatedBy = HttpContext.User.FindFirst("username")?.Value,
                PreviousState = JsonSerializer.Serialize(new { model.LocationCode, model.LocationType, model.RackId, model.WarehouseId }),
                Comment = $"删除库位 {model.LocationCode}"
            };
            _db.LocationOps.Add(deleteOp);

            _repository.Delete(id);
            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取库位地图基础数据：仓库列表 + 指定仓库的货架列表
    /// </summary>
    [HttpGet("Map")]
    public Result GetLocationMap(int? warehouseId = null)
    {
        try
        {
            var warehouses = _db.Warehouses.OrderBy(w => w.Id).ToList();
            var currentWarehouseId = warehouseId ?? warehouses.FirstOrDefault()?.Id;
            if (currentWarehouseId == null) return Result.Fail("无仓库数据");

            var racks = _db.Racks
                .Where(r => r.WarehouseId == currentWarehouseId)
                .OrderBy(r => r.RackCode)
                .Select(r => new { r.RackId, r.RackCode })
                .ToList();

            return Result<object>.Success(new { warehouses, currentWarehouseId, racks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取库位地图数据失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取仓库货架汇总统计（轻量）
    /// </summary>
    [HttpGet("Map/Summary")]
    public Result GetRackSummary(int warehouseId)
    {
        try
        {
            var summary = _db.Locations
                .Where(l => l.WarehouseId == warehouseId && l.LocationType == "R")
                .GroupBy(l => l.RackId)
                .Select(g => new
                {
                    RackId = g.Key,
                    Total = g.Count(),
                    Occupied = g.Count(l => l.xExists && l.UnitloadCount > 0),
                    InboundDisabled = g.Count(l => l.InboundDisabled),
                    OutboundDisabled = g.Count(l => l.OutboundDisabled),
                    NonExist = g.Count(l => !l.xExists)
                })
                .OrderBy(s => s.RackId)
                .ToList();

            var rackCodes = _db.Racks
                .Where(r => summary.Select(s => s.RackId).Contains(r.RackId))
                .ToDictionary(r => r.RackId, r => r.RackCode);

            var result = summary.Select(s => new
            {
                s.RackId,
                RackCode = rackCodes.TryGetValue(s.RackId!.Value, out var code) ? code : string.Empty,
                s.Total,
                s.Occupied,
                s.InboundDisabled,
                s.OutboundDisabled,
                s.NonExist,
                Available = s.Total - s.Occupied - s.InboundDisabled - s.OutboundDisabled
            });

            return Result<object>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取货架汇总失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取单个货架的库位网格数据
    /// </summary>
    [HttpGet("Map/Rack/{rackId}")]
    public Result GetRackGrid(int rackId)
    {
        try
        {
            var rack = _db.Racks.Find(rackId);
            if (rack == null) return Result.Fail("货架不存在");

            var locations = _db.Locations
                .Where(l => l.RackId == rackId && l.LocationType == "R")
                .ToList();

            var cells = locations.Select(l => new LocationCellDto
            {
                LocationId = l.LocationId,
                LocationCode = l.LocationCode,
                xLevel = l.xLevel,
                xColumn = l.xColumn,
                xExists = l.xExists,
                UnitloadCount = l.UnitloadCount,
                InboundDisabled = l.InboundDisabled,
                OutboundDisabled = l.OutboundDisabled,
                InboundCount = l.InboundCount,
                OutboundCount = l.OutboundCount,
                InboundLimit = l.InboundLimit,
                OutboundLimit = l.OutboundLimit,
                StorageGroup = l.StorageGroup,
                SubStorageGroup = l.SubStorageGroup
            }).ToList();

            return Result<object>.Success(new
            {
                RackId = rack.RackId,
                RackCode = rack.RackCode,
                Cells = cells
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取货架网格失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 批量更新库位状态
    /// </summary>
    [HttpPut("Map/Batch")]
    public Result BatchUpdateLocations([FromBody] BatchUpdateLocationRequest request)
    {
        try
        {
            if (request.LocationIds == null || request.LocationIds.Length == 0)
                return Result.Fail("请选择要更新的库位");

            var locations = _db.Locations
                .Where(l => request.LocationIds.Contains(l.LocationId))
                .ToList();

            if (locations.Count == 0) return Result.Fail("未找到匹配的库位");

            foreach (var loc in locations)
            {
                // 记录变更前的状态
                var prevState = JsonSerializer.Serialize(new
                {
                    loc.LocationCode, loc.InboundDisabled, loc.OutboundDisabled,
                    loc.xExists, loc.StorageGroup, loc.SubStorageGroup
                });

                if (request.InboundDisabled.HasValue)
                    loc.InboundDisabled = request.InboundDisabled.Value;
                if (request.OutboundDisabled.HasValue)
                    loc.OutboundDisabled = request.OutboundDisabled.Value;
                if (request.xExists.HasValue)
                    loc.xExists = request.xExists.Value;
                if (request.UnitloadCount.HasValue)
                    loc.UnitloadCount = request.UnitloadCount.Value;
                if (request.StorageGroup != null)
                    loc.StorageGroup = request.StorageGroup;
                if (request.SubStorageGroup != null)
                    loc.SubStorageGroup = request.SubStorageGroup;
                loc.ModifiedTime = DateTime.Now;

                var newState = JsonSerializer.Serialize(new
                {
                    loc.LocationCode, loc.InboundDisabled, loc.OutboundDisabled,
                    loc.xExists, loc.StorageGroup, loc.SubStorageGroup
                });

                _db.LocationOps.Add(new LocationOp
                {
                    LocationId = loc.LocationId,
                    OpType = "BatchUpdate",
                    Url = HttpContext.Request.Path,
                    CreatedTime = DateTime.UtcNow,
                    CreatedBy = HttpContext.User.FindFirst("username")?.Value,
                    PreviousState = prevState,
                    NewState = newState,
                    Comment = $"批量更新库位 {loc.LocationCode}"
                });
            }

            _db.SaveChanges();
            return Result.Success("批量更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量更新库位失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

}
