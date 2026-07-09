using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.Handlers.WcsRequest;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Persistence;
using WcsRequestDto = Wms.Core.Application.DTOs.WcsRequest;

namespace Wms.Core.Infrastructure.Handlers.WcsRequest;

/// <summary>
/// 工艺验证工位请求处理器 — 比较多个托盘工艺是否一致，不下发 WCS
/// 流程：验证容器存在 → 取工艺 → 比较 → 返回 WcsResult
/// </summary>
public class VerfiyProcessRequestHandler : IWcsRequestHandler
{
    private readonly WmsDbContext _db;
    private readonly ILogger<VerfiyProcessRequestHandler> _logger;

    /// <summary>
    ///
    /// </summary>
    public string RequestType => Cst.工艺验证;

    /// <summary>
    ///
    /// </summary>
    /// <param name="db"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public VerfiyProcessRequestHandler(
        WmsDbContext db,
        ILogger<VerfiyProcessRequestHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="request"></param>
    /// <param name="location"></param>
    /// <returns></returns>
    public async Task<WcsResult> HandleAsync(WcsRequestDto request, Location location)
    {
        location = await _db.Locations.FindAsync(location.LocationId) ?? location;

        _logger.LogInformation("[WcsRequest] 工艺验证: 位置={Location}, 容器={Container}",
            location.LocationCode, string.Join(",", request.ContainerCode ?? []));

        if (request.ContainerCode == null || request.ContainerCode.Length < 2)
            return ApiResultHelper.WcsFail("工艺验证至少需要两个容器编码", ResultCodeTypes.数据异常, -1);

        var codes = request.ContainerCode
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToArray();

        if (codes.Length < 2)
            return ApiResultHelper.WcsFail("有效容器编码至少需要两个", ResultCodeTypes.数据异常, -1);

        // 1. 批量查询所有 Unitload
        var unitloads = await _db.Unitloads
            .Where(u => codes.Contains(u.ContainerCode))
            .ToListAsync();

        // 2. 验证所有容器都存在
        foreach (var code in codes)
        {
            if (unitloads.All(u => u.ContainerCode != code))
                return ApiResultHelper.WcsFail($"托盘 {code} 不存在", ResultCodeTypes.数据异常, -1);
        }

        // 3. 取每个托盘的 CurrentOperation，比较是否全部一致
        var operations = unitloads.Select(u =>
            u.CurrentOperation ?? string.Empty).ToList();

        var allSame = operations.Distinct(StringComparer.OrdinalIgnoreCase).Count() <= 1;

        if (!allSame)
        {
            var details = unitloads.Select(u =>
                $"{u.ContainerCode}({u.CurrentOperation})");
            _logger.LogWarning("[WcsRequest] 工艺验证不通过: {Details}",
                string.Join(", ", details));
            return ApiResultHelper.WcsFail(
                $"托盘工艺不一致: {string.Join(", ", details)}",
                ResultCodeTypes.排废批次不同, -1);
        }

        // 4. 更新所有 Unitload 的当前位置和到位时间
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var ul in unitloads)
            {
                ul.LocationId = location.LocationId;
                ul.CurrentLocationTime = DateTime.Now;
            }
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        _logger.LogInformation("[WcsRequest] 工艺验证通过，已更新位置: {Containers}→{Loc}",
            string.Join(",", codes), location.LocationCode);

        return ApiResultHelper.WcsSuccess("工艺验证通过", ResultCodeTypes.一, 1);
    }
}
