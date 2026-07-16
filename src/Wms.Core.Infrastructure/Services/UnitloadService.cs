using Microsoft.EntityFrameworkCore;
using Wms.Core.Domain.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Archive;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Extensions;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Requests;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Tasks;
using Wms.Core.Infrastructure.Handlers.WcsRequest;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Persistence.Repositories;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 
/// </summary>
public class UnitloadService : IUnitloadService
{

    public static readonly string 异常电芯批次 = CommonTypes.异常电芯批次;
    public static readonly string 电芯码空 = CommonTypes.电芯码空;

    public static readonly string 正常 = Unitload_Enum.UnitloadItemDetailStatus.正常.ToString();
    public static readonly string 假电芯 = Unitload_Enum.UnitloadItemDetailStatus.假电芯.ToString();
    public static readonly string 混批 = Unitload_Enum.UnitloadItemDetailStatus.混批.ToString();
    public static readonly string 混型 = Unitload_Enum.UnitloadItemDetailStatus.混型.ToString();
    public static readonly string 重码 = Unitload_Enum.UnitloadItemDetailStatus.重码.ToString();
    public static readonly string 漏码 = Unitload_Enum.UnitloadItemDetailStatus.漏码.ToString();
    public static readonly string NG = Unitload_Enum.UnitloadItemDetailStatus.NG.ToString();
    public static readonly string 无档位 = Unitload_Enum.UnitloadItemDetailStatus.无档位.ToString();
    public static readonly string 混档 = Unitload_Enum.UnitloadItemDetailStatus.混档.ToString();
    public static readonly string 条码异常 = Unitload_Enum.UnitloadItemDetailStatus.条码异常.ToString();

    public static readonly string 电芯异常 = Unitload_Enum.UnitLoadErrMsg.电芯异常.ToString();
    public static readonly string 换盘异常 = Unitload_Enum.UnitLoadErrMsg.换盘异常.ToString();

    public static readonly string 假电芯前缀格式 = CommonTypes.假电芯前缀格式;

    private readonly WmsDbContext _db;
    private readonly ILogger<UnitloadService> _logger;
    private readonly IRepository<Unitload, int> _repository;
    private readonly ITranslationService _translationService;
    private readonly IBasicDictionaryService _basicDictionaryService;
    private readonly ILocationService _locationService;
    private readonly IMemoryCache _cache;
    private readonly IProcessRouteService _processRouteService;

    /// <summary>
    ///
    /// </summary>
    /// <param name="db"></param>
    /// <param name="repository"></param>
    /// <param name="translationService"></param>
    /// <param name="basicDictionaryService"></param>
    /// <param name="locationService"></param>
    /// <param name="logger"></param>
    /// <param name="cache"></param>
    /// <param name="processRouteService"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public UnitloadService(
        WmsDbContext db,
        IRepository<Unitload, int> repository,
        ITranslationService translationService,
        IBasicDictionaryService basicDictionaryService,
        ILocationService locationService,
        ILogger<UnitloadService> logger,
        IMemoryCache cache,
        IProcessRouteService processRouteService
        )
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        _basicDictionaryService = basicDictionaryService ?? throw new ArgumentNullException(nameof(basicDictionaryService));
        _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _processRouteService = processRouteService ?? throw new ArgumentNullException(nameof(processRouteService));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<Result> CreateUnitloadManual(UnitloadRequest request)
    {
        // 1.参数非空验证
        if (request == null)
            return Result.Fail("请求参数不能为空");

        if (request.ContainerCode == null || request.ContainerCode.Length == 0)
            return Result.Fail("容器编码不能为空");

        if (string.IsNullOrEmpty(request.CurrentOperation))
            return Result.Fail("当前工艺不能为空");

        if (request.MaterialId.HasValue && request.MaterialId.Value == 0)
            return Result.Fail("物料不能为空");

        if (request.Items == null || request.Items.Count == 0)
            return Result.Fail("电芯集合不能为空");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var containerCode = request.ContainerCode[0];

            // 2.查询基础数据 PRODUCTIONMATERIAL 集合，匹配 Name 与 request.CurrentOperation 记录
            var productionMaterialParents = _basicDictionaryService.GetItemsByNo("PRODUCTIONMATERIAL");

            var materialDict = productionMaterialParents.FirstOrDefault(x => x.Name == request.CurrentOperation);

            if (materialDict == null)
                return Result.Fail($"未找到工艺 {request.CurrentOperation} 对应的物料配置");

            // 3.根据基础数据的 expandField1 与 request.Items 数量验证
            if (!int.TryParse(materialDict.ExpandField1, out int expectedCount))
                return Result.Fail("物料配置的电芯数量无效");

            if (request.Items == null || request.Items.Count != expectedCount)
                return Result.Fail($"电芯数量不匹配，期望 {expectedCount}，实际 {request.Items?.Count ?? 0}");

            // 3.1 根据基础数据的 Value 查询物料信息
            var material = _db.Set<Materials>()
                .FirstOrDefault(m => m.MaterialCode == materialDict.Value);
            if (material == null)
                return Result.Fail($"物料编码 {materialDict.Value} 不存在");

            // 3.2 根据 request.LocationCode 查询位置信息
            int locationId = 0;
            if (!string.IsNullOrEmpty(request.LocationCode))
            {
                var location = _locationService.GetLocation(request.LocationCode);
                if (location == null)
                    return Result.Fail($"库位 {request.LocationCode} 不存在");
                locationId = location.LocationId;
            }
            else
            {
                var location = _locationService.GetLocation(Cst.None);
                if (location == null)
                    return Result.Fail($"库位 {request.LocationCode} 不存在");
                locationId = location.LocationId;
            }

            // 4.验证托盘码和箱码是否重复
            if (_repository.Exists(u => u.ContainerCode == containerCode))
                return Result.Fail("容器编码已存在");

            if (_db.Set<UnitloadItem>().Any(ui => ui.BoxCode == containerCode))
                return Result.Fail("箱码已存在于托盘明细中");

            var now = DateTime.Now;

            // 5.创建 Unitload
            var unitload = new Unitload
            {
                ContainerCode = containerCode,
                CurrentOperation = request.CurrentOperation,
                Version = 0,
                OperationNumber = request.OperationNumber ?? 1,
                IsAdvance = request.IsAdvance ?? 0,
                LocationId = locationId,

                StorageGroup = Cst.普通,
                OutFlag = string.Empty,
                ContainerSpecification = Cst.普通托盘,

                IsExcludeCurrentUnitload = false,
                IsUpload = false,
                IsToHangke = 0,
                CurrentLocationTime = now,
                CreatedTime = now,
                ModifiedTime = now,
                CreatedBy = request.CreatedBy
            };

            // 5.1 根据 CurrentOperation 和 Unitload_Enum.CurrentOperation 枚举查询 NextOperation
            unitload.NextOperation = GetNextOperation(request.CurrentOperation);

            _db.Set<Unitload>().Add(unitload);
            _db.SaveChanges();

            // 5.1.1 绑定工艺路线（双模式：有匹配路线则绑定，否则保持硬编码模式）
            await _processRouteService.BindRouteAsync(unitload, material.MaterialId);

            // 5.2 创建 UnitloadItem
            var unitloadItem = new UnitloadItem
            {
                UnitloadId = unitload.UnitloadId,
                MaterialId = material.MaterialId,
                Quantity = request.Items.Count,
                Uom = material.Uom,
                OperationNumber = request.OperationNumber ?? 1,
                BoxCode = containerCode,
                ProductionTime = now,
                IsAdvance = request.IsAdvance ?? 0,
                xLevel = request.Level
            };
            _db.Set<UnitloadItem>().Add(unitloadItem);
            _db.SaveChanges();

            // 5.5 电芯条码状态验证 - 先检查请求集合中重码
            var barcodesInRequest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in request.Items)
            {
                if (string.IsNullOrEmpty(item.BatteryCode) || item.BatteryCode == 电芯码空)
                {
                    if (AllowsEmptyBarcode(request.CurrentOperation))
                        continue;
                    throw new InvalidRequestException(电芯码空 + $"：位置 {item.LocIndex} 电芯 {item.BatteryCode}");
                }
                else if (!string.IsNullOrEmpty(item.BatteryCode) && item.BatteryCode != 电芯码空)
                {
                    if (!barcodesInRequest.Add(item.BatteryCode))
                        throw new InvalidRequestException(重码 + $"：位置 {item.LocIndex} 电芯 {item.BatteryCode}");
                }
            }

            // 检查 UnitloadItemDetail 表中重码
            var requestBarcodes = request.Items
                .Where(i => !string.IsNullOrEmpty(i.BatteryCode) && i.BatteryCode != 电芯码空 && !IsFakeBarcode(i.BatteryCode))
                .Select(i => i.BatteryCode)
                .ToList();

            if (requestBarcodes.Count > 0)
            {
                var existingBarcodes = _db.Set<UnitloadItemDetail>()
                    .Where(d => requestBarcodes.Contains(d.BarCode))
                    .Select(d => new { d.BarCode, d.UnitloadItemId })
                    .ToList();

                if (existingBarcodes.Count > 0)
                    throw new InvalidRequestException(重码 + $"：电芯 {string.Join(",", existingBarcodes.Select(e => e.BarCode))} 已存在于明细中");
            }

            var batchMap = new Dictionary<string, int>();
            var fakePrefix = _basicDictionaryService.GetByNo(假电芯前缀格式)?.Value ?? string.Empty;
            int fakeCount = 0;

            // 5.3 根据 request.Items.Count 数量循环创建 UnitloadItemDetail
            // 5.4 记录每支电芯批次
            for (int i = 0; i < request.Items.Count; i++)
            {
                var batteryItem = request.Items[i];

                var detail = new UnitloadItemDetail
                {
                    UnitloadItemId = unitloadItem.UnitloadItemId,
                    LocIndex = batteryItem.LocIndex,
                    Status = 正常
                };

                if (batteryItem == null
                    || string.IsNullOrEmpty(batteryItem.BatteryCode)
                    || batteryItem.BatteryCode == 电芯码空)
                {
                    if (AllowsEmptyBarcode(request.CurrentOperation))
                        continue;
                    throw new InvalidRequestException(漏码 + $"：位置 {i}");
                }

                detail.BarCode = batteryItem.BatteryCode;

                // 条码长度异常
                if (batteryItem.BatteryCode.Length != 24)
                    throw new InvalidRequestException(条码异常 + $"：位置 {i} 电芯 {batteryItem.BatteryCode}");

                // 假电芯检测
                bool isFake = false;
                if (!string.IsNullOrEmpty(fakePrefix) && batteryItem.BatteryCode.StartsWith(fakePrefix))
                {
                    detail.Status = 假电芯;
                    isFake = true;
                    fakeCount++;
                }
                else if (IsFakeBarcode(batteryItem.BatteryCode))
                {
                    detail.Status = 假电芯;
                    isFake = true;
                    fakeCount++;
                }

                // 获取批次（假电芯跳过）
                if (!isFake)
                {
                    var batch = GetBatchFromBarcode(batteryItem.BatteryCode);
                    if (!string.IsNullOrEmpty(batch))
                    {
                        if (batchMap.ContainsKey(batch))
                            batchMap[batch]++;
                        else
                            batchMap[batch] = 1;
                    }
                }

                _db.Set<UnitloadItemDetail>().Add(detail);
            }

            // 更新实际电芯数量（空条码被跳过后修正 Quantity）
            unitloadItem.Quantity = request.Items.Count(i => !string.IsNullOrEmpty(i.BatteryCode) && i.BatteryCode != 电芯码空);

            // 假电芯工艺校验
            if (!AllowsFakeBarcode(request.CurrentOperation) && fakeCount > 0)
            {
                throw new InvalidRequestException(假电芯 + $"：工艺 {request.CurrentOperation} 不允许假电芯，共 {fakeCount} 支");
            }

            // 记录假电芯数量
            unitloadItem.FalseQuantity = fakeCount;

            string _unitItemBatch = batchMap.OrderByDescending(x => x.Value).FirstOrDefault().Key;
            unitloadItem.Batch = _unitItemBatch;
            unitloadItem.OutOrdering = _unitItemBatch;    //_outOrderingProvider.GetOutOrdering(unitloadItem);

            _db.SaveChanges();

            // 混批检查
            if (batchMap.Count > 1)
            {
                throw new InvalidRequestException(混批 + $"：{string.Join(",", batchMap.Keys)}");
            }

            // 6.添加托盘操作日志
            AddUnitloadOp(containerCode, UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.入库.ToString(), createdBy: request.CreatedBy);

            _db.SaveChanges();
            await transaction.CommitAsync();

            return Result<Unitload>.Success(unitload, "创建成功");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "创建货载失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 更新货载（条码明细 + 可选容器编码）
    /// </summary>
    public async Task<Result> UpdateUnitload(UpdateUnitloadRequest request)
    {
        if (request == null)
            return Result.Fail("请求参数不能为空");

        if (request.UnitloadItems == null || request.UnitloadItems.Count == 0)
            return Result.Fail("物料明细不能为空");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var unitload = _db.Set<Unitload>()
                .Include(u => u.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                .FirstOrDefault(u => u.UnitloadId == request.UnitloadId);

            if (unitload == null)
                return Result.Fail("货载不存在");

            if (unitload.BeingMoved == true)
                return Result.Fail("货载正在移动中，无法更新");

            if (_db.Set<TransTask>().Any(t => t.UnitloadId == unitload.UnitloadId && t.ForWcs == true && t.WasSentToWcs != true))
                return Result.Fail("货载有任务执行中，无法更新");

            var unitItemIds = unitload.UnitloadItems.Select(ui => ui.UnitloadItemId).ToHashSet();
            foreach (var item in request.UnitloadItems)
            {
                if (!unitItemIds.Contains(item.UnitloadItemId))
                    return Result.Fail($"物料明细ID {item.UnitloadItemId} 不属于该货载");
            }

            if (!string.IsNullOrEmpty(request.NewContainerCode))
            {
                if (_db.Set<Unitload>().Any(u => u.UnitloadId != request.UnitloadId && u.ContainerCode == request.NewContainerCode))
                    return Result.Fail($"容器编码 {request.NewContainerCode} 已存在");
                if (_db.Set<UnitloadItem>().Any(ui => ui.UnitloadId != request.UnitloadId && ui.BoxCode == request.NewContainerCode))
                    return Result.Fail($"箱码 {request.NewContainerCode} 已存在");
            }

            // === 阶段二：数据验证 ===
            var fakePrefix = _basicDictionaryService.GetByNo(假电芯前缀格式)?.Value ?? string.Empty;
            var itemsToDelete = new List<int>();
            var allUnitItemIds = unitload.UnitloadItems.Select(ui => ui.UnitloadItemId).ToList();

            foreach (var reqItem in request.UnitloadItems)
            {
                var items = reqItem.Items ?? new List<UnitloadRequestItem>();

                if (items.Count == 0 || items.All(i => string.IsNullOrEmpty(i.BatteryCode) || i.BatteryCode == 电芯码空))
                {
                    itemsToDelete.Add(reqItem.UnitloadItemId);
                    continue;
                }

                // 不允许空条码的工艺验证（一注装盘、高温浸润、化成重组装盘）
                if (!AllowsEmptyBarcode(unitload.CurrentOperation))
                {
                    var emptyPositions = items
                        .Where(i => string.IsNullOrEmpty(i.BatteryCode) || i.BatteryCode == 电芯码空)
                        .Select(i => i.LocIndex)
                        .ToList();
                    if (emptyPositions.Count > 0)
                        throw new InvalidRequestException(电芯码空 + $"：位置 {string.Join(",", emptyPositions)}");
                }

                var barcodesInRequest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.BatteryCode) || item.BatteryCode == 电芯码空)
                        continue;
                    if (!barcodesInRequest.Add(item.BatteryCode))
                        throw new InvalidRequestException(重码 + $"：位置 {item.LocIndex} 电芯 {item.BatteryCode}");
                }

                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.BatteryCode) || item.BatteryCode == 电芯码空)
                        continue;
                    if (item.BatteryCode.Length != 24)
                        throw new InvalidRequestException(条码异常 + $"：位置 {item.LocIndex} 电芯 {item.BatteryCode}");
                    if ((!string.IsNullOrEmpty(fakePrefix) && item.BatteryCode.StartsWith(fakePrefix)) || IsFakeBarcode(item.BatteryCode))
                        continue;
                    if (_db.Set<UnitloadItemDetail>().Any(d => d.BarCode == item.BatteryCode && !allUnitItemIds.Contains(d.UnitloadItemId ?? 0)))
                        throw new InvalidRequestException(重码 + $"：电芯 {item.BatteryCode} 已存在于明细中");
                }

                var batchMap = new Dictionary<string, int>();
                int fakeCount = 0;
                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.BatteryCode) || item.BatteryCode == 电芯码空 || item.BatteryCode.Length != 24)
                        continue;
                    bool isFake = (!string.IsNullOrEmpty(fakePrefix) && item.BatteryCode.StartsWith(fakePrefix)) || IsFakeBarcode(item.BatteryCode);
                    if (isFake) { fakeCount++; continue; }
                    var batch = GetBatchFromBarcode(item.BatteryCode);
                    if (!string.IsNullOrEmpty(batch))
                    {
                        if (batchMap.ContainsKey(batch)) batchMap[batch]++;
                        else batchMap[batch] = 1;
                    }
                }
                if (batchMap.Count > 1)
                    throw new InvalidRequestException(混批 + $"：{string.Join(",", batchMap.Keys)}");
                if (!AllowsFakeBarcode(unitload.CurrentOperation) && fakeCount > 0)
                    throw new InvalidRequestException(假电芯 + $"：工艺 {unitload.CurrentOperation} 不允许假电芯，共 {fakeCount} 支");
            }

            // === 阶段三：执行更新 ===
            foreach (var reqItem in request.UnitloadItems)
            {
                var unitItem = unitload.UnitloadItems.FirstOrDefault(ui => ui.UnitloadItemId == reqItem.UnitloadItemId);
                if (unitItem == null) continue;

                _db.Set<UnitloadItemDetail>().RemoveRange(
                    _db.Set<UnitloadItemDetail>().Where(d => d.UnitloadItemId == unitItem.UnitloadItemId));

                if (itemsToDelete.Contains(unitItem.UnitloadItemId))
                    continue;

                var items = reqItem.Items ?? new List<UnitloadRequestItem>();
                var batchMap = new Dictionary<string, int>();
                int fakeCount = 0;

                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.BatteryCode) || item.BatteryCode == 电芯码空)
                        continue;

                    var detail = new UnitloadItemDetail
                    {
                        UnitloadItemId = unitItem.UnitloadItemId,
                        LocIndex = item.LocIndex ?? 0,
                        BarCode = item.BatteryCode,
                        Status = 正常
                    };

                    bool isFake = (!string.IsNullOrEmpty(fakePrefix) && item.BatteryCode.StartsWith(fakePrefix))
                        || IsFakeBarcode(item.BatteryCode);
                    if (isFake) { detail.Status = 假电芯; fakeCount++; }
                    else
                    {
                        var batch = GetBatchFromBarcode(item.BatteryCode);
                        if (!string.IsNullOrEmpty(batch))
                        {
                            if (batchMap.ContainsKey(batch)) batchMap[batch]++;
                            else batchMap[batch] = 1;
                        }
                    }

                    _db.Set<UnitloadItemDetail>().Add(detail);
                }

                unitItem.Quantity = items.Count(i => !string.IsNullOrEmpty(i.BatteryCode) && i.BatteryCode != 电芯码空);
                unitItem.FalseQuantity = fakeCount;
                unitItem.Batch = batchMap.OrderByDescending(x => x.Value).FirstOrDefault().Key;
                unitItem.OutOrdering = unitItem.Batch;
            }

            if (itemsToDelete.Count > 0)
            {
                _db.Set<UnitloadItem>().RemoveRange(
                    unitload.UnitloadItems.Where(ui => itemsToDelete.Contains(ui.UnitloadItemId)));
            }

            if (!string.IsNullOrEmpty(request.NewContainerCode))
            {
                var oldCode = unitload.ContainerCode;
                unitload.ContainerCode = request.NewContainerCode;
                foreach (var ui in unitload.UnitloadItems)
                    ui.BoxCode = request.NewContainerCode;
                AddUnitloadOp(oldCode, UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.其他.ToString(),
                    comment: $"更新容器编码: {oldCode} → {request.NewContainerCode}", createdBy: request.ModifiedBy);
            }

            unitload.ModifiedTime = DateTime.Now;
            unitload.ModifiedBy = request.ModifiedBy;
            unitload.HasCountingError = false;
            unitload.HasMsgError = null;

            _db.SaveChanges();
            await transaction.CommitAsync();

            return Result<Unitload>.Success(unitload, "更新成功");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "更新货载失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<Result> CreateUnitloadAutomatic(WcsRequest request)
    {
        // 1.参数非空验证
        if (request == null)
            return Result.Fail("请求参数不能为空");

        if (request.ContainerCode == null || request.ContainerCode.Length == 0)
            return Result.Fail("容器编码不能为空");

        if (request.Battery == null || request.Battery.Count == 0)
            return Result.Fail("电芯集合不能为空");

        if (string.IsNullOrWhiteSpace(request.LocationCode))
            return Result.Fail("位置编码不能为空");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var containerCode = request.ContainerCode[0];

            // 3.2 根据 request.LocationCode 查询位置信息
            int locationId = 0;
            Location location = null;

            location = _locationService.GetLocation(request.LocationCode);
            if (location == null)
                return Result.Fail($"库位 {request.LocationCode} 不存在");
            locationId = location.LocationId;

            // 2.查询基础数据 PRODUCTIONMATERIAL 集合，匹配 Name 与 request.CurrentOperation 记录
            var productionMaterialParents = _basicDictionaryService.GetItemsByNo("PRODUCTIONMATERIAL");

            var materialDict = productionMaterialParents.FirstOrDefault(x => x.Name == location.Tag);

            if (materialDict == null)
                return Result.Fail($"未找到工艺 {location.Tag} 对应的物料配置");

            // 3.根据基础数据的 expandField1 与 request.Items 数量验证
            if (!int.TryParse(materialDict.ExpandField1, out int expectedCount))
                return Result.Fail("物料配置的电芯数量无效");

            if (request.Battery == null || request.Battery.Count != expectedCount)
                return Result.Fail($"电芯数量不匹配，期望 {expectedCount}，实际 {request.Battery?.Count ?? 0}");

            // 3.1 根据基础数据的 Value 查询物料信息
            var material = _db.Set<Materials>()
                .FirstOrDefault(m => m.MaterialCode == materialDict.Value);
            if (material == null)
                return Result.Fail($"物料编码 {materialDict.Value} 不存在");

            // 4.验证托盘码和箱码是否重复，重复时追加时间戳
            if (_repository.Exists(u => u.ContainerCode == containerCode)
                || _db.Set<UnitloadItem>().Any(ui => ui.BoxCode == containerCode))
            {
                containerCode = $"{containerCode}_{DateTime.Now:yyyyMMddHHmmss}";
            }

            var now = DateTime.Now;

            // 5.创建 Unitload
            var unitload = new Unitload
            {
                ContainerCode = containerCode,
                CurrentOperation = location.Tag,
                Version = 0,
                OperationNumber = 1,
                IsAdvance = 0,
                LocationId = locationId,

                StorageGroup = Cst.普通,
                OutFlag = string.Empty,
                ContainerSpecification = Cst.普通托盘,

                IsExcludeCurrentUnitload = false,
                IsUpload = false,
                IsToHangke = 0,
                CurrentLocationTime = now,
                CreatedTime = now,
                ModifiedTime = now,
                CreatedBy = "System"
            };

            // 5.1 根据 CurrentOperation 和 Unitload_Enum.CurrentOperation 枚举查询 NextOperation
            unitload.NextOperation = GetNextOperation(location.Tag ?? "一注装盘");

            _db.Set<Unitload>().Add(unitload);
            _db.SaveChanges();

            // 5.1.1 绑定工艺路线（双模式：有匹配路线则绑定，否则保持硬编码模式）
            await _processRouteService.BindRouteAsync(unitload, material.MaterialId);

            // 5.2 创建 UnitloadItem
            var unitloadItem = new UnitloadItem
            {
                UnitloadId = unitload.UnitloadId,
                MaterialId = material.MaterialId,
                Quantity = request.Battery.Count,
                Uom = material.Uom,
                OperationNumber = unitload.OperationNumber,
                BoxCode = containerCode,
                ProductionTime = now,
                IsAdvance = unitload.IsAdvance,
                xLevel = string.Empty
            };
            _db.Set<UnitloadItem>().Add(unitloadItem);
            _db.SaveChanges();

            // 5.5 电芯条码状态验证 - 先检查请求集合中重码
            var barcodesInRequest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var batchMap = new Dictionary<string, int>();
            var fakePrefix = _basicDictionaryService.GetByNo(假电芯前缀格式)?.Value ?? string.Empty;
            int fakeCount = 0;

            // 5.6 处理电芯批次（跳过假电芯）
            foreach (KeyValuePair<int, string> item in request.Battery.OrderBy(s => s.Key))
            {
                bool isFakeBarcodeItem = (!string.IsNullOrEmpty(fakePrefix) && item.Value.StartsWith(fakePrefix))
                    || IsFakeBarcode(item.Value);
                if (!isFakeBarcodeItem)
                {
                    var batch = GetBatchFromBarcode(item.Value);
                    if (!string.IsNullOrEmpty(batch))
                    {
                        if (batchMap.ContainsKey(batch))
                            batchMap[batch]++;
                        else
                            batchMap[batch] = 1;
                    }
                }
            }

            string _unitItemBatch = batchMap.OrderByDescending(x => x.Value).FirstOrDefault().Key;
            unitloadItem.Batch = _unitItemBatch;
            unitloadItem.OutOrdering = _unitItemBatch;

            bool ngFlag = false;
            StringBuilder ngMessage = new StringBuilder();

            // 循环前：一次查库
            var allValidBarcodes = request.Battery
                .Where(kv => !string.IsNullOrEmpty(kv.Value) && kv.Value != 电芯码空 && kv.Value.Length == 24 && !IsFakeBarcode(kv.Value))
                .Select(kv => kv.Value)
                .ToList();

            var existingDbBarcodes = new HashSet<string>(
                _db.Set<UnitloadItemDetail>()
                    .Where(d => allValidBarcodes.Contains(d.BarCode))
                    .Select(d => d.BarCode),
                StringComparer.OrdinalIgnoreCase);

            // 5.3 根据 request.Battery.Count 数量循环创建 UnitloadItemDetail
            // 5.4 记录每支电芯批次，标识状态信息
            foreach (KeyValuePair<int, string> item in request.Battery.OrderBy(s => s.Key))
            {
                var detail = new UnitloadItemDetail
                {
                    UnitloadItemId = unitloadItem.UnitloadItemId,
                    LocIndex = item.Key,
                    BarCode = item.Value,
                    Status = 正常
                };

                // 所有电芯明细都保存到数据库
                _db.Set<UnitloadItemDetail>().Add(detail);

                if (string.IsNullOrEmpty(item.Value)
                    || item.Value == 电芯码空)
                {
                    if (AllowsEmptyBarcode(unitload.CurrentOperation))
                    {
                        ngFlag = true;
                        detail.Status = 漏码;
                        ngMessage.Append(漏码 + $"：位置 {item.Key}");
                        continue;
                    }
                    throw new InvalidRequestException(电芯码空 + $"：位置 {item.Key}");
                }

                // 条码长度异常
                if (item.Value.Length != 24)
                {
                    ngFlag = true;
                    detail.Status = 条码异常;
                    ngMessage.Append(条码异常 + $"：位置 {item.Key} 电芯 {item.Value}");
                    continue;
                }

                // 假电芯
                if ((!string.IsNullOrEmpty(fakePrefix) && item.Value.StartsWith(fakePrefix))
                    || IsFakeBarcode(item.Value))
                {
                    fakeCount++;
                    detail.Status = 假电芯;
                    continue;
                }

                // 批次
                var batch = GetBatchFromBarcode(item.Value);
                if (_unitItemBatch != null && !batch.Equals(_unitItemBatch))
                {
                    ngFlag = true;
                    detail.Status = 混批;
                    ngMessage.Append(混批 + $"：位置 {item.Key} 电芯 {item.Value}");
                    continue;
                }

                // 验证 item.Value 在 request.Battery 中是否重码
                if (barcodesInRequest.Contains(item.Value))
                {
                    ngFlag = true;
                    detail.Status = 重码;
                    ngMessage.Append(重码 + $"：位置 {item.Key} 电芯 {item.Value}");
                    continue;
                }
                barcodesInRequest.Add(item.Value);

                // 循环内替换原来的逐条查询
                if (existingDbBarcodes.Contains(item.Value))
                {
                    ngFlag = true;
                    detail.Status = 重码;
                    ngMessage.Append(重码 + $"：位置 {item.Key} 电芯 {item.Value}");
                    continue;
                }
            }

            _db.SaveChanges();

            // 假电芯工艺校验
            if (!AllowsFakeBarcode(unitload.CurrentOperation) && fakeCount > 0)
            {
                ngFlag = true;
                ngMessage.Append(假电芯 + $"：工艺 {unitload.CurrentOperation} 不允许假电芯，共 {fakeCount} 支");
            }

            // 记录假电芯数量
            unitloadItem.FalseQuantity = fakeCount;

            if (ngFlag)
            {
                unitload.HasCountingError = ngFlag;
                unitload.HasMsgError = ngMessage.ToString();
            }


            _db.SaveChanges();

            // 6.添加托盘操作日志
            AddUnitloadOp(containerCode, UnitloadOps_Enum.OpType.自动.ToString(), UnitloadOps_Enum.Direction.入库.ToString(), createdBy: "WCS");

            _db.SaveChanges();
            await transaction.CommitAsync();

            return Result<Unitload>.Success(unitload, "创建成功");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "创建货载失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 允许假电芯的工艺列表
    /// </summary>
    private static readonly HashSet<string> AllowFakeOperations = new()
    {
        "一注装盘",
        "高温浸润",
        "化成重组装盘"
    };

    /// <summary>
    /// 判断工艺是否允许假电芯
    /// </summary>
    /// <param name="operation"></param>
    /// <returns></returns>
    private bool AllowsFakeBarcode(string? operation)
    {
        return !string.IsNullOrEmpty(operation) && AllowFakeOperations.Contains(operation);
    }

    /// <summary>
    /// 判断是否为24个"1"假电芯
    /// </summary>
    /// <param name="barcode"></param>
    /// <returns></returns>
    private bool IsFakeBarcode(string barcode)
    {
        return !string.IsNullOrEmpty(barcode) && barcode.All(c => c == '1');
    }

    /// <summary>
    /// 判断工艺是否允许空条码（清洗装盘、分容重组装盘、成品）
    /// </summary>
    private bool AllowsEmptyBarcode(string? operation)
    {
        return !string.IsNullOrEmpty(operation) && !AllowsFakeBarcode(operation);
    }

    /// <summary>
    /// 根据当前工艺获取下一工艺
    /// </summary>
    public string? GetNextOperation(string currentOperation)
    {
        if (string.IsNullOrEmpty(currentOperation))
            return null;

        // 重组装盘 → 对应主线工艺
        if (currentOperation == Unitload_Enum.CurrentOperation.化成重组装盘.ToString()) return Unitload_Enum.CurrentOperation.化成.ToString();
        if (currentOperation == Unitload_Enum.CurrentOperation.分容重组装盘.ToString()) return Unitload_Enum.CurrentOperation.分容.ToString();

        if (Enum.TryParse<Unitload_Enum.CurrentOperation>(currentOperation, out var currentOpEnum))
        {
            var nextValue = (int)currentOpEnum + 1;
            if (Enum.IsDefined(typeof(Unitload_Enum.CurrentOperation), nextValue))
                return ((Unitload_Enum.CurrentOperation)nextValue).ToString();
        }

        return null;
    }

    /// <summary>
    /// 从电芯条码获取批次：第5位取3个，第14位开始取4个，共7位
    /// </summary>
    public string? GetBatchFromBarcode(string barcode)
    {
        if (string.IsNullOrEmpty(barcode) || barcode.Length < 24)
            return string.Empty;

        try
        {
            return barcode.Substring(5, 3) + barcode.Substring(14, 4);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 添加托盘操作日志（公用方法）
    /// </summary>
    /// <param name="containerCode">容器编码</param>
    /// <param name="opType">操作类型（人工/自动/化成/分容/OCV3/OCV4/DCIR）</param>
    /// <param name="direction">方向（入库/出库/叠盘/拆盘/其他/移动）</param>
    /// <param name="comment">备注</param>
    public void AddUnitloadOp(string containerCode, string opType, string direction, string? comment = null, string? createdBy = null)
    {
        LocationAllocator.AddUnitloadOp(_db, containerCode, opType, direction, comment, createdBy);
    }

    /// <summary>
    /// 生成随机电芯条码（24位格式）
    /// </summary>
    public Dictionary<int, string> GenerateBatteryBarcodes(int number, int month, int day, int start)
    {
        string[] months = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C"];
        string[] days = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "P", "R", "S", "T", "V", "W", "X", "Y", "0"];
        var battery = new Dictionary<int, string>();

        for (int i = 0; i < number; i++)
        {
            var builder = new System.Text.StringBuilder();
            builder.Append("03H");      // X1-X3 厂家代码
            builder.Append("C");        // X4 电芯
            builder.Append("B");        // X5 电池类型：磷酸铁锂
            builder.Append("L22");      // X6-X8 产品代码
            builder.Append("DC1");      // X9-X11 批次码
            builder.Append("0");        // X12 0 量产 S 实验
            builder.Append("D");        // X13 产线代码
            builder.Append("K");        // X14 经开代码
            builder.Append("E" + months[month - 1] + days[day - 1]); // X15-17 年月日
            builder.Append("1");        // X18 生产特征

            int _xh = start + i;
            int _len = _xh.ToString().Length;
            for (int j = 0; j < (6 - _len); j++)
                builder.Append("0");
            builder.Append(_xh.ToString());

            battery.Add(i + 1, builder.ToString());
        }

        return battery;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="containerCode"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public bool IsUnitloadExist(string containerCode)
    {
        if (string.IsNullOrEmpty(containerCode))
            return false;

        return _repository.Exists(u => u.ContainerCode == containerCode)
            || _db.Set<UnitloadItem>().Any(ui => ui.BoxCode == containerCode);
    }

    /// <summary>
    /// 归档
    /// </summary>
    /// <param name="unitloadId"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<Result> Archive(int unitloadId, string? modifiedBy = null)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 1.查询 Unitload 及其子表
            var unitload = _db.Set<Unitload>()
                .Include(u => u.Location)
                .Include(u => u.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                .FirstOrDefault(u => u.UnitloadId == unitloadId);

            if (unitload == null)
                return Result.Fail("货载不存在");

            // 2.验证：是否正在移动
            if (unitload.BeingMoved == true)
                return Result.Fail("货载正在移动中，无法归档");

            // 3.验证：当前位置类型是否为货架
            if (unitload.Location != null && unitload.Location.LocationType == Location_Enum.LocationType.R.ToString())
                return Result.Fail("货载在货架上，无法归档");

            // 4.验证：是否有未下发的WCS任务
            if (_db.Set<TransTask>().Any(t => t.UnitloadId == unitloadId && t.ForWcs == true && t.WasSentToWcs != true))
                return Result.Fail("货载有任务执行中，无法归档");

            var now = DateTime.Now;

            // 4.1 清理 FK 引用（ArchiveUnitload 不含此步骤，必须先做；与 CleanupEmptyTrayItemsAsync 保持一致）
            var containerCodeForCleanup = unitload.ContainerCode ?? string.Empty;
            _db.Database.ExecuteSqlRaw(
                "UPDATE Flows SET UnitloadId = NULL WHERE UnitloadId = {0}", unitloadId);
            _db.Database.ExecuteSqlRaw(
                "UPDATE Stocks SET UnitloadId = NULL WHERE UnitloadId = {0}", unitloadId);
            _db.Database.ExecuteSqlRaw(
                "UPDATE TransTasks SET UnitloadId = NULL, UnitloadCode = {1} WHERE UnitloadId = {0}",
                unitloadId, containerCodeForCleanup);
            _db.Database.ExecuteSqlRaw(
                "UPDATE UnionUnitloadItems SET UnitloadId = NULL WHERE UnitloadId = {0}", unitloadId);

            // 5.归档（拷贝到 Archive 表 + 删除原始数据）
            LocationAllocator.ArchiveUnitload(_db, unitload, modifiedBy ?? "人工归档", unitload.ModifiedBy);

            // 6.记录操作日志
            AddUnitloadOp(unitload.ContainerCode ?? "", UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.其他.ToString(), "归档", modifiedBy);

            _db.SaveChanges();
            await transaction.CommitAsync();
            return Result.Success("归档成功");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "归档失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 还原
    /// </summary>
    /// <param name="unitloadId"></param>
    /// <returns></returns>
    public async Task<Result> Recover(int unitloadId, string? modifiedBy = null)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 1.查询归档数据
            var archivedUnitload = _db.Set<ArchivedUnitload>()
                .FirstOrDefault(a => a.Id == unitloadId);

            if (archivedUnitload == null)
                return Result.Fail("归档记录不存在");

            // 2.验证：容器编码在 Unitload 中是否重复
            if (!string.IsNullOrEmpty(archivedUnitload.ContainerCode))
            {
                if (_repository.Exists(u => u.ContainerCode == archivedUnitload.ContainerCode)
                    || _db.Set<UnitloadItem>().Any(ui => ui.BoxCode == archivedUnitload.ContainerCode))
                    return Result.Fail($"容器编码 {archivedUnitload.ContainerCode} 已存在，无法还原");
            }

            var now = DateTime.Now;

            // 3.拷贝 ArchivedUnitload → Unitload
            var unitload = new Unitload
            {
                ContainerCode = archivedUnitload.ContainerCode,
                CreatedTime = archivedUnitload.CreatedTime,
                CreatedBy = archivedUnitload.CreatedBy,
                Weight = archivedUnitload.Weight,
                Height = archivedUnitload.Height,
                Length = archivedUnitload.Length,
                Width = archivedUnitload.Width,
                Volume = archivedUnitload.Volume,
                StorageGroup = archivedUnitload.StorageGroup,
                OutFlag = archivedUnitload.OutFlag,
                ContainerSpecification = archivedUnitload.ContainerSpecification,
                HasCountingError = archivedUnitload.HasCountingError,
                HasMsgError = archivedUnitload.HasMsgError,
                LocationId = archivedUnitload.LocationId,
                CurrentLocationTime = now,
                OpHintType = archivedUnitload.OpHintType,
                OpHintInfo = archivedUnitload.OpHintInfo,
                OperationNumber = archivedUnitload.OperationNumber,
                CurrentOperation = archivedUnitload.CurrentOperation,
                NextOperation = archivedUnitload.NextOperation,
                IsExcludeCurrentUnitload = archivedUnitload.IsExcludeCurrentUnitload,
                IsUpload = archivedUnitload.IsUpload,
                IsAdvance = archivedUnitload.IsAdvance,
                IsSupplement = archivedUnitload.IsSupplement,
                IsToHangke = archivedUnitload.IsToHangke,
                ProcessRouteId = archivedUnitload.ProcessRouteId,
                ProcessRouteVersionId = archivedUnitload.ProcessRouteVersionId,
                CurrentStepId = archivedUnitload.CurrentStepId,
                NextStepId = archivedUnitload.NextStepId,
                IsAwaitingBranchSelection = archivedUnitload.IsAwaitingBranchSelection,
                Version = 0
            };
            _db.Set<Unitload>().Add(unitload);
            _db.SaveChanges();

            // 4.拷贝 ArchivedUnitloadItem → UnitloadItem
            var archivedItems = _db.Set<ArchivedUnitloadItem>()
                .Where(ai => ai.UnitloadId == archivedUnitload.Id)
                .ToList();

            foreach (var archivedItem in archivedItems)
            {
                var item = new UnitloadItem
                {
                    UnitloadId = unitload.UnitloadId,
                    MaterialId = archivedItem.MaterialId,
                    Batch = archivedItem.Batch,
                    StockStatus = archivedItem.StockStatus,
                    Quantity = archivedItem.Quantity,
                    FalseQuantity = archivedItem.FalseQuantity,
                    Uom = archivedItem.Uom,
                    ProductionTime = archivedItem.ProductionTime,
                    OutOrdering = archivedItem.OutOrdering,
                    BoxCode = archivedItem.BoxCode,
                    Position = archivedItem.Position,
                    xLevel = archivedItem.xLevel,
                    OperationNumber = archivedItem.OperationNumber,
                    BatchNumber = archivedItem.BatchNumber,
                    IsAdvance = archivedItem.IsAdvance,
                    IsSupplement = archivedItem.IsSupplement
                };
                _db.Set<UnitloadItem>().Add(item);
                _db.SaveChanges();

                // 5.拷贝 ArchivedUnitloadItemDetail → UnitloadItemDetail
                var archivedDetails = _db.Set<ArchivedUnitloadItemDetail>()
                    .Where(ad => ad.UnitloadItemId == archivedItem.Id)
                    .ToList();

                foreach (var archivedDetail in archivedDetails)
                {
                    var detail = new UnitloadItemDetail
                    {
                        UnitloadItemId = item.UnitloadItemId,
                        BarCode = archivedDetail.BarCode,
                        xLevel = archivedDetail.xLevel,
                        OCV3 = archivedDetail.OCV3,
                        IR3 = archivedDetail.IR3,
                        V3KeYa = archivedDetail.V3KeYa,
                        OCV4 = archivedDetail.OCV4,
                        IR4 = archivedDetail.IR4,
                        V4KeYa = archivedDetail.V4KeYa,
                        Capacity = archivedDetail.Capacity,
                        KVal = archivedDetail.KVal,
                        CCP = archivedDetail.CCP,
                        Dcirnz = archivedDetail.Dcirnz,
                        Sequence = archivedDetail.Sequence,
                        Comment = archivedDetail.Comment,
                        LocIndex = archivedDetail.LocIndex,
                        Status = archivedDetail.Status
                    };
                    _db.Set<UnitloadItemDetail>().Add(detail);
                }
            }

            _db.SaveChanges();

            // 6.删除归档数据
            var archivedItemIds = archivedItems.Select(i => i.Id).ToList();
            if (archivedItemIds.Any())
            {
                _db.Set<ArchivedUnitloadItemDetail>()
                    .RemoveRange(_db.Set<ArchivedUnitloadItemDetail>().Where(d => archivedItemIds.Contains(d.UnitloadItemId!.Value)));
            }
            _db.Set<ArchivedUnitloadItem>()
                .RemoveRange(archivedItems);
            _db.Set<ArchivedUnitload>().Remove(archivedUnitload);

            // 7.记录操作日志
            AddUnitloadOp(unitload.ContainerCode ?? "", UnitloadOps_Enum.OpType.人工.ToString(), UnitloadOps_Enum.Direction.其他.ToString(), "还原", modifiedBy);

            _db.SaveChanges();
            await transaction.CommitAsync();
            return Result.Success("还原成功");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "还原失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 删除
    /// </summary>
    /// <param name="unitloadId"></param>
    /// <returns></returns>
    public async Task<Result> Delete(int unitloadId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var unitload = _db.Set<Unitload>()
                .Include(u => u.Location)
                .Include(u => u.UnitloadItems).ThenInclude(ui => ui.UnitloadItemDetails)
                .FirstOrDefault(u => u.UnitloadId == unitloadId);

            if (unitload == null)
                return Result.Fail("货载不存在");

            if (unitload.BeingMoved == true)
                return Result.Fail("货载正在移动中，无法删除");

            // 当前位置类型是不是货架
            if (unitload.Location != null && unitload.Location.LocationType == Location_Enum.LocationType.R.ToString())
                return Result.Fail("货载在货架上，无法删除");

            // 货载有没有未下发的WCS任务
            if (_db.Set<TransTask>().Any(t => t.UnitloadId == unitloadId && t.ForWcs == true && t.WasSentToWcs != true))
                return Result.Fail("货载有任务执行中，无法删除");

            // 清理 FK 引用（与 CleanupEmptyTrayItemsAsync 保持一致，否则触发 FK_Flows_Unitload 等约束冲突）
            var containerCodeForCleanup = unitload.ContainerCode ?? string.Empty;
            _db.Database.ExecuteSqlRaw(
                "UPDATE Flows SET UnitloadId = NULL WHERE UnitloadId = {0}", unitloadId);
            _db.Database.ExecuteSqlRaw(
                "UPDATE Stocks SET UnitloadId = NULL WHERE UnitloadId = {0}", unitloadId);
            _db.Database.ExecuteSqlRaw(
                "UPDATE TransTasks SET UnitloadId = NULL, UnitloadCode = {1} WHERE UnitloadId = {0}",
                unitloadId, containerCodeForCleanup);
            _db.Database.ExecuteSqlRaw(
                "UPDATE UnionUnitloadItems SET UnitloadId = NULL WHERE UnitloadId = {0}", unitloadId);

            var itemIds = unitload.UnitloadItems?.Select(ui => ui.UnitloadItemId).ToList() ?? [];
            if (itemIds.Any())
            {
                _db.Set<UnitloadItemDetail>()
                    .RemoveRange(_db.Set<UnitloadItemDetail>().Where(d => itemIds.Contains(d.UnitloadItemId!.Value)));
            }
            _db.Set<UnitloadItem>()
                .RemoveRange(_db.Set<UnitloadItem>().Where(i => i.UnitloadId == unitloadId));
            _db.Set<Unitload>().Remove(unitload);
            _db.SaveChanges();

            await transaction.CommitAsync();
            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "删除失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }
}
