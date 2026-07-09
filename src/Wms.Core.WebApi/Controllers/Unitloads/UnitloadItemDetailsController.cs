using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Repositories;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Extensions;

namespace Wms.Core.WebApi.Controllers.Unitloads;

/// <summary>
/// 电芯明细管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class UnitloadItemDetailsController : ControllerBase
{
    private readonly WmsDbContext _db;
    private readonly ILogger<UnitloadItemDetailsController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UnitloadItemDetailsController(
        WmsDbContext db,
        ILogger<UnitloadItemDetailsController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取电芯明细列表
    /// </summary>
    /// <param name="barCode">电芯条码（可选）</param>
    /// <param name="boxCode">托盘码（可选）</param>
    /// <param name="batch">批次（可选）</param>
    /// <param name="level">档位（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? barCode = null, string? boxCode = null, string? batch = null, string? level = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _db.Set<UnitloadItemDetail>()
                .Include(d => d.UnitloadItem)
                .AsQueryable();

            if (!string.IsNullOrEmpty(barCode))
            {
                query = query.Where(d => d.BarCode != null && d.BarCode.Contains(barCode));
            }

            if (!string.IsNullOrEmpty(boxCode))
            {
                query = query.Where(d => d.UnitloadItem != null && d.UnitloadItem.BoxCode == boxCode);
            }

            if (!string.IsNullOrEmpty(batch))
            {
                query = query.Where(d => d.UnitloadItem != null && d.UnitloadItem.Batch == batch);
            }

            if (!string.IsNullOrEmpty(level))
            {
                query = query.Where(d => d.xLevel == level);
            }

            var totalCount = query.Count();

            var lists = query
                .OrderByDescending(d => d.UnitloadItemDetailId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.UnitloadItemDetailId,
                    d.UnitloadItemId,
                    d.BarCode,
                    d.xLevel,
                    d.OCV3,
                    d.IR3,
                    d.V3KeYa,
                    d.OCV4,
                    d.IR4,
                    d.V4KeYa,
                    d.Capacity,
                    d.KVal,
                    d.CCP,
                    d.Dcirnz,
                    d.Sequence,
                    d.LocIndex,
                    d.Status,
                    d.Comment,
                    BoxCode = d.UnitloadItem != null ? d.UnitloadItem.BoxCode : null,
                    Batch = d.UnitloadItem != null ? d.UnitloadItem.Batch : null,
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
            _logger.LogError(ex, "获取电芯明细列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 批量检查条码是否已存在于 UnitloadItemDetails 中
    /// </summary>
    [HttpPost("check-barcodes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result CheckBarcodes([FromBody] CheckBarcodesRequest request)
    {
        try
        {
            if (request.Barcodes == null || request.Barcodes.Count == 0)
                return Result<object>.Success(new { existingBarcodes = Array.Empty<string>() });

            var existingBarcodes = _db.Set<UnitloadItemDetail>()
                .Where(d => request.Barcodes.Contains(d.BarCode))
                .Select(d => d.BarCode!)
                .Distinct()
                .ToList();

            return Result<object>.Success(new { existingBarcodes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查条码失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }
}

public class CheckBarcodesRequest
{
    public List<string> Barcodes { get; set; } = new();
}
