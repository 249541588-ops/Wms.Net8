using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Application.Handlers.WcsRequest;
using Wms.Core.Application.Ports;
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
/// 托盘类型验证工位请求处理器 — 验证托盘类型，不下发 WCS
/// 流程：验证容器存在 → 验证托盘类型 → 返回 WcsResult
/// </summary>
public class VerfiyPalletTypeRequestHandler : IWcsRequestHandler
{
    private readonly WmsDbContext _db;
    private readonly IBasicDictionaryService _basicDictionaryService;
    private readonly ILogger<VerfiyPalletTypeRequestHandler> _logger;

    /// <summary>
    ///
    /// </summary>
    public string RequestType => Cst.托盘类型验证;

    /// <summary>
    ///
    /// </summary>
    /// <param name="db"></param>
    /// <param name="basicDictionaryService"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public VerfiyPalletTypeRequestHandler(
        WmsDbContext db,
        IBasicDictionaryService basicDictionaryService,
        ILogger<VerfiyPalletTypeRequestHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _basicDictionaryService = basicDictionaryService ?? throw new ArgumentNullException(nameof(basicDictionaryService));
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
        // 1. 验证 ContainerCode：需要且仅需要 1 个
        if (request.ContainerCode == null || request.ContainerCode.Length != 1
            || string.IsNullOrWhiteSpace(request.ContainerCode[0]))
            return ApiResultHelper.WcsFail("托盘类型验证需要且仅需要一个容器编码", ResultCodeTypes.数据异常, -1);

        var containerCode = request.ContainerCode[0];

        // 2. 查询 location，若不存在则返回失败
        var loc = await _db.Locations.FindAsync(location.LocationId);
        if (loc == null)
            return ApiResultHelper.WcsFail($"库位 {location.LocationCode} 不存在", ResultCodeTypes.数据异常, -1);
        location = loc;

        _logger.LogInformation("[WcsRequest] 托盘类型验证: 位置={Location}, 容器={Container}",
            location.LocationCode, containerCode);

        // 3. 读取后台字典映射（VERFIYPALLETTYPE_MAP：子项 Name=工序名 → Value=resultcode），含 30 分钟内存缓存
        var mappings = _basicDictionaryService.GetItemsByNo(Cst.托盘类型验证映射);

        // 4. 查询 Unitload
        var unitload = await _db.Unitloads
            .FirstOrDefaultAsync(u => u.ContainerCode == containerCode);

        // 5. 若托盘不存在，取字典中 NOT_EXIST 子项的 Value（保底 ResultCodeTypes.一）
        if (unitload == null)
        {
            var notExistCode = mappings
                .FirstOrDefault(x => x.No == Cst.托盘类型验证_NOT_EXIST)?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(notExistCode))
                notExistCode = ResultCodeTypes.一;

            _logger.LogInformation(
                "[WcsRequest] 托盘 {Container} 不存在，返回 resultcode={Code}（来源：字典 NOT_EXIST）",
                containerCode, notExistCode);
            return ApiResultHelper.WcsSuccess("托盘不存在", notExistCode, 1);
        }

        // 6. 判断当前工艺，映射到结果编码（resultcode）—— 从后台字典读取
        var op = (unitload.CurrentOperation ?? string.Empty).Trim();
        var matched = mappings.FirstOrDefault(x =>
            string.Equals((x.Name ?? string.Empty).Trim(), op, StringComparison.OrdinalIgnoreCase));

        if (matched == null || string.IsNullOrWhiteSpace(matched.Value))
        {
            return ApiResultHelper.WcsFail(
                $"托盘 {containerCode} 当前工艺「{op}」未在字典 {Cst.托盘类型验证映射} 中配置映射",
                ResultCodeTypes.数据异常, -1);
        }

        var resultCode = matched.Value.Trim();

        // 7. 更新 Unitload 位置和到位时间
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            unitload.LocationId = location.LocationId;
            unitload.CurrentLocationTime = DateTime.Now;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        _logger.LogInformation(
            "[WcsRequest] 托盘类型验证通过: {Container} 工艺={Op} → 命中字典 {DictNo} → resultcode={Code}",
            containerCode, op, matched.No, resultCode);

        // 8. 返回成功结果（resultCode 承载工艺状态码，currentoperation 固定 1）
        return ApiResultHelper.WcsSuccess($"托盘工艺: {op}", resultCode, 1);
    }
}
