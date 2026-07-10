using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Repositories;
using Wms.Core.Application.Ports;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Controllers.Unitloads;

/// <summary>
/// 货载管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
//[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class UnitloadsController : ControllerBase
{
    private readonly IRepository<Unitload, int> _repository;
    private readonly IUnitloadService _unitloadService;
    private readonly WmsDbContext _db;
    private readonly ILogger<UnitloadsController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UnitloadsController(
        IRepository<Unitload, int> repository,
        IUnitloadService unitloadService,
        WmsDbContext db,
        ILogger<UnitloadsController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitloadService = unitloadService ?? throw new ArgumentNullException(nameof(unitloadService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }       

    /// <summary>
    /// 获取所有菜单
    /// </summary>
    /// <param name="containerCode">容器编码（可选）</param>
    /// <param name="batch">批次（可选）</param>
    /// <param name="currentOperation">当前工艺（可选）</param>
    /// <param name="operationNumber">工艺次数（可选）</param>
    /// <param name="barCode">电芯码（可选，模糊匹配）</param>
    /// <param name="beingMoved">是否移动（可选）</param>
    /// <param name="allocated">是否分配（可选）</param>
    /// <param name="hasCountingError">是否异常（可选）</param>
    /// <param name="locationCode">当前位置（可选，模糊匹配）</param>
    /// <param name="warehouseId">所属库区 ID（可选）</param>
    /// <param name="materialCode">物料编码（可选）</param>
    /// <param name="currentLocationTimeStart">当前位置时间-开始（可选）</param>
    /// <param name="currentLocationTimeEnd">当前位置时间-结束（可选）</param>
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
        bool? beingMoved = null,
        bool? allocated = null,
        bool? hasCountingError = null,
        string? locationCode = null,
        int? warehouseId = null,
        string? materialCode = null,
        DateTime? currentLocationTimeStart = null,
        DateTime? currentLocationTimeEnd = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            // 限制每页最大大小
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

            // 批次在 UnitloadItem 上，通过子查询过滤
            if (!string.IsNullOrEmpty(batch))
            {
                var matchedIds = _db.Set<UnitloadItem>()
                    .Where(i => i.Batch == batch)
                    .Select(i => i.UnitloadId)
                    .Distinct()
                    .ToList();
                query = query.Where(m => matchedIds.Contains(m.UnitloadId));
            }

            // 电芯码在 UnitloadItemDetail 上，通过子查询过滤（跨 3 层实体）
            if (!string.IsNullOrEmpty(barCode))
            {
                var matchedIds = _db.Set<UnitloadItemDetail>()
                    .Where(d => d.BarCode != null && d.BarCode.Contains(barCode))
                    .Select(d => d.UnitloadItem.UnitloadId)
                    .Distinct()
                    .ToList();
                query = query.Where(m => matchedIds.Contains(m.UnitloadId));
            }

            // 物料编码在 UnitloadItem.Material 上
            if (!string.IsNullOrEmpty(materialCode))
            {
                var matchedIds = _db.Set<UnitloadItem>()
                    .Where(i => i.Material != null && i.Material.MaterialCode == materialCode)
                    .Select(i => i.UnitloadId)
                    .Distinct()
                    .ToList();
                query = query.Where(m => matchedIds.Contains(m.UnitloadId));
            }

            // 直接字段过滤
            if (beingMoved.HasValue)
            {
                query = query.Where(m => m.BeingMoved == beingMoved.Value);
            }

            if (allocated.HasValue)
            {
                query = query.Where(m => m.Allocated == allocated.Value);
            }

            if (hasCountingError.HasValue)
            {
                query = query.Where(m => m.HasCountingError == hasCountingError.Value);
            }

            // 通过 Location 导航属性过滤（EF Core 会生成 LEFT JOIN）
            if (!string.IsNullOrEmpty(locationCode))
            {
                query = query.Where(m => m.Location != null && m.Location.LocationCode != null && m.Location.LocationCode.Contains(locationCode));
            }

            if (warehouseId.HasValue)
            {
                query = query.Where(m => m.Location != null && m.Location.WarehouseId == warehouseId.Value);
            }

            // 时间范围
            if (currentLocationTimeStart.HasValue)
            {
                query = query.Where(m => m.CurrentLocationTime >= currentLocationTimeStart.Value);
            }

            if (currentLocationTimeEnd.HasValue)
            {
                query = query.Where(m => m.CurrentLocationTime <= currentLocationTimeEnd.Value);
            }

            // 获取总数
            var totalCount = query.Count();

            // 分页
            var lists = query
                .Include(m => m.Location)
                .Include(m => m.UnitloadItems).ThenInclude(ui => ui.Material)
                .OrderBy(m => m.UnitloadId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.UnitloadId,
                    m.Version,
                    m.ContainerCode,
                    m.CurrentOperation,
                    m.NextOperation,
                    m.OperationNumber,
                    m.IsAdvance,
                    m.IsSupplement,
                    m.CurrentLocationTime,
                    m.OpHintType,
                    m.OpHintInfo,
                    m.BeingMoved,
                    m.Allocated,
                    m.HasMsgError,
                    m.HasCountingError,
                    m.CreatedBy,
                    m.CreatedTime,
                    m.ModifiedTime,
                    m.ModifiedBy,
                    LocationCode = m.Location != null ? m.Location.LocationCode : null,
                    Batch = m.UnitloadItems != null ? m.UnitloadItems.FirstOrDefault()!.Batch : null,
                    BatteryCount = m.UnitloadItems != null ? (int?)m.UnitloadItems.Sum(ui => ui.Quantity) : null,
                    MaterialCode = m.UnitloadItems != null ? m.UnitloadItems.FirstOrDefault()!.Material!.MaterialCode : null,
                    MaterialName = m.UnitloadItems != null ? (m.UnitloadItems.FirstOrDefault()!.Material!.Description+"【"+ m.UnitloadItems.FirstOrDefault()!.Material!.MaterialCode + "】") : null
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
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 获取待入库货载：当前位置 LocationType 非 R（非货架库位），或 Location 为 null
    /// </summary>
    /// <param name="containerCode">容器编码（可选）</param>
    /// <param name="batch">批次（可选）</param>
    /// <param name="currentOperation">当前工艺（可选）</param>
    /// <param name="operationNumber">工艺次数（可选）</param>
    /// <param name="barCode">电芯码（可选，模糊匹配）</param>
    /// <param name="beingMoved">是否移动（可选）</param>
    /// <param name="allocated">是否分配（可选）</param>
    /// <param name="hasCountingError">是否异常（可选）</param>
    /// <param name="locationCode">当前位置（可选，模糊匹配）</param>
    /// <param name="materialCode">物料编码（可选）</param>
    /// <param name="currentLocationTimeStart">当前位置时间-开始（可选）</param>
    /// <param name="currentLocationTimeEnd">当前位置时间-结束（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet("Pending")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetPending(
        string? containerCode = null,
        string? batch = null,
        string? currentOperation = null,
        int? operationNumber = null,
        string? barCode = null,
        bool? beingMoved = null,
        bool? allocated = null,
        bool? hasCountingError = null,
        string? locationCode = null,
        string? materialCode = null,
        DateTime? currentLocationTimeStart = null,
        DateTime? currentLocationTimeEnd = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _db.Set<Unitload>()
                .Include(m => m.Location)
                .Where(m => m.Location == null || m.Location.LocationType != "R");

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

            if (!string.IsNullOrEmpty(batch))
            {
                var matchedIds = _db.Set<UnitloadItem>()
                    .Where(i => i.Batch == batch)
                    .Select(i => i.UnitloadId)
                    .Distinct()
                    .ToList();
                query = query.Where(m => matchedIds.Contains(m.UnitloadId));
            }

            // 电芯码：跨 3 层实体子查询（导航属性已注册）
            if (!string.IsNullOrEmpty(barCode))
            {
                var matchedIds = _db.Set<UnitloadItemDetail>()
                    .Where(d => d.BarCode != null && d.BarCode.Contains(barCode))
                    .Select(d => d.UnitloadItem.UnitloadId)
                    .Distinct()
                    .ToList();
                query = query.Where(m => matchedIds.Contains(m.UnitloadId));
            }

            // 物料编码：通过 UnitloadItem.Material 导航属性
            if (!string.IsNullOrEmpty(materialCode))
            {
                var matchedIds = _db.Set<UnitloadItem>()
                    .Where(i => i.Material != null && i.Material.MaterialCode == materialCode)
                    .Select(i => i.UnitloadId)
                    .Distinct()
                    .ToList();
                query = query.Where(m => matchedIds.Contains(m.UnitloadId));
            }

            // 直接字段过滤
            if (beingMoved.HasValue)
            {
                query = query.Where(m => m.BeingMoved == beingMoved.Value);
            }

            if (allocated.HasValue)
            {
                query = query.Where(m => m.Allocated == allocated.Value);
            }

            if (hasCountingError.HasValue)
            {
                query = query.Where(m => m.HasCountingError == hasCountingError.Value);
            }

            // 通过 Location 导航属性过滤（初始 query 已 Include Location）
            if (!string.IsNullOrEmpty(locationCode))
            {
                query = query.Where(m => m.Location != null && m.Location.LocationCode != null && m.Location.LocationCode.Contains(locationCode));
            }

            // 时间范围
            if (currentLocationTimeStart.HasValue)
            {
                query = query.Where(m => m.CurrentLocationTime >= currentLocationTimeStart.Value);
            }

            if (currentLocationTimeEnd.HasValue)
            {
                query = query.Where(m => m.CurrentLocationTime <= currentLocationTimeEnd.Value);
            }

            var totalCount = query.Count();

            var lists = query
                .Include(m => m.UnitloadItems).ThenInclude(ui => ui.Material)
                .OrderBy(m => m.UnitloadId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.UnitloadId,
                    m.Version,
                    m.ContainerCode,
                    m.CurrentOperation,
                    m.NextOperation,
                    m.OperationNumber,
                    m.IsAdvance,
                    m.IsSupplement,
                    m.CurrentLocationTime,
                    m.OpHintType,
                    m.OpHintInfo,
                    m.BeingMoved,
                    m.Allocated,
                    m.HasMsgError,
                    m.HasCountingError,
                    m.CreatedBy,
                    m.CreatedTime,
                    m.ModifiedTime,
                    m.ModifiedBy,
                    LocationCode = m.Location != null ? m.Location.LocationCode : null,
                    Batch = m.UnitloadItems != null ? m.UnitloadItems.FirstOrDefault()!.Batch : null,
                    BatteryCount = m.UnitloadItems != null ? (int?)m.UnitloadItems.Sum(ui => ui.Quantity) : null,
                    MaterialCode = m.UnitloadItems != null ? m.UnitloadItems.FirstOrDefault()!.Material!.MaterialCode : null,
                    MaterialName = m.UnitloadItems != null ? m.UnitloadItems.FirstOrDefault()!.Material!.Description : null
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
            _logger.LogError(ex, "获取待入库货载失败: {Message}", ex.Message);
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
            var model = _db.Set<Unitload>()
                .Include(m => m.Location)
                .Include(m => m.UnitloadItems).ThenInclude(ui => ui.Material)
                .Include(m => m.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                .FirstOrDefault(m => m.UnitloadId == id);

            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            return Result<Unitload>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 按容器编码获取货载
    /// </summary>
    [HttpGet("by-code/{containerCode}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetByCode(string containerCode)
    {
        try
        {
            var model = _db.Set<Unitload>()
                .Include(m => m.Location)
                .Include(m => m.UnitloadItems).ThenInclude(ui => ui.Material)
                .Include(m => m.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                .FirstOrDefault(m => m.ContainerCode == containerCode);

            if (model == null)
            {
                return Result.Fail("货载不存在");
            }

            return Result<Unitload>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "按容器编码获取货载失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 更新货载（条码明细 + 可选容器编码）
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Result> Update(int id, [FromBody] UpdateUnitloadRequest request)
    {
        try
        {
            request.UnitloadId = id;
            return await _unitloadService.UpdateUnitload(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新货载失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
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
    public async Task<Result> Create([FromBody] UnitloadRequest request)
    {
       return await _unitloadService.CreateUnitloadManual(request);
    }

    /// <summary>
    /// 检查容器编码是否已存在
    /// </summary>
    /// <param name="containerCode">容器编码</param>
    /// <returns>是否存在</returns>
    [HttpGet("exist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result IsExist(string containerCode)
    {
        var exists = _unitloadService.IsUnitloadExist(containerCode);
        return Result<object>.Success(new { exists }, exists ? "已存在" : "不存在");
    }


    /// <summary>
    /// 删除
    /// </summary>
    /// <param name="id">货载 ID</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Result> Delete(int id)
    {
        return await _unitloadService.Delete(id);
    }

    /// <summary>
    /// 归档
    /// </summary>
    /// <param name="id">货载 ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("{id:int}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Result> Archive(int id, string? modifiedBy = null)
    {
        return await _unitloadService.Archive(id, modifiedBy);
    }

    /// <summary>
    /// 还原
    /// </summary>
    /// <param name="id">归档记录 ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("{id:int}/recover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Result> Recover(int id, string? modifiedBy = null)
    {
        return await _unitloadService.Recover(id, modifiedBy);
    }


}
