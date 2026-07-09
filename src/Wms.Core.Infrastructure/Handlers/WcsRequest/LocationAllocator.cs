using System.Threading;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Archive;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Entities.StockFlow;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Tasks;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Handlers.WcsRequest;

/// <summary>
/// 公共货位分配器 — 封装入库/移库共用的货位分配逻辑
/// </summary>
public class LocationAllocator
{
    private readonly WmsDbContext _db;
    private readonly LocationAllocationEngine _engine;
    private readonly ILogger<LocationAllocator> _logger;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="db"></param>
    /// <param name="engine"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public LocationAllocator(
        WmsDbContext db,
        LocationAllocationEngine engine,
        ILogger<LocationAllocator> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 根据起点位置和托盘信息，分配目标货位
    /// </summary>
    /// <param name="location">起点位置（包含 LanewayCodes）</param>
    /// <param name="unitload">托盘对象（提供存储信息）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>分配到的目标库位，失败返回 null</returns>
    public async Task<Location?> AllocateAsync(Location location, Unitload unitload, CancellationToken ct = default)
    {
        var lanewayCodes = ParseLanewayCodes(location.LanewayCodes);

        if (lanewayCodes.Length == 0)
        {
            // R 类型（货架位）：通过 Rack -> Laneway 获取巷道编码（同巷道分配）
            if (location.LocationType == "R" && location.RackId != null)
            {
                var rack = await _db.Racks.FindAsync(location.RackId.Value, ct);
                if (rack?.LanewayId != null)
                {
                    var laneway = await _db.Laneways.FindAsync(rack.LanewayId.Value, ct);
                    if (laneway != null && !string.IsNullOrWhiteSpace(laneway.LanewayCode))
                    {
                        lanewayCodes = [laneway.LanewayCode];
                    }
                }
            }
        }

        if (lanewayCodes.Length == 0)
        {
            _logger.LogWarning("[库位分配] 位置 {LocationCode} 无可用巷道编码", location.LocationCode);
            return null;
        }

        var storageInfo = BuildStorageInfo(unitload);

        foreach (var lanewayCode in lanewayCodes)
        {
            ct.ThrowIfCancellationRequested();

            var laneway = await _db.Laneways
                .FirstOrDefaultAsync(l => l.LanewayCode == lanewayCode, ct);

            if (laneway == null)
            {
                _logger.LogWarning("[库位分配] 巷道编码 {LanewayCode} 不存在", lanewayCode);
                continue;
            }

            if (laneway.Offline == true)
            {
                _logger.LogWarning("[库位分配] 巷道 {LanewayCode} 已离线，跳过", lanewayCode);
                continue;
            }

            if (laneway.Automated != true)
            {
                _logger.LogWarning("[库位分配] 巷道 {LanewayCode} 非自动化，跳过", lanewayCode);
                continue;
            }

            var doubleDeep = laneway.DoubleDeep ?? false;

            var connection = _db.Database.GetDbConnection();
            var targetLocation = await _engine.AllocateAsync(
                connection,
                laneway.LanewayId,
                doubleDeep,
                storageInfo,
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<int>(),
                "xColumn, xLevel",
                "asc");

            if (targetLocation != null)
            {
                return targetLocation;
            }
        }

        _logger.LogWarning("[库位分配] 所有巷道均未命中，位置 {LocationCode}", location.LocationCode);
        return null;
    }

    /// <summary>
    /// 临近货位分配 — 优先同层邻列，找不到回退普通分配
    /// </summary>
    /// <param name="referenceLocation">参考位置（第一个托盘的目标位置）</param>
    /// <param name="startLocation">起点位置（包含 LanewayCodes）</param>
    /// <param name="unitload">托盘对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>分配到的目标库位，失败返回 null</returns>
    public async Task<Location?> AllocateNearbyAsync(
        Location referenceLocation, Location startLocation, Unitload unitload,
        CancellationToken ct = default)
    {
        if (referenceLocation.RackId == null)
        {
            _logger.LogInformation("[库位分配] 参考位置 RackId 为空，回退普通分配");
            return await AllocateAsync(startLocation, unitload, ct);
        }

        var storageInfo = BuildStorageInfo(unitload);

        try
        {
            var connection = _db.Database.GetDbConnection();
            var nearby = await connection.QueryFirstOrDefaultAsync<Location?>(@"
                SELECT TOP 1 loc.*
                FROM Locations loc
                JOIN Cells c ON c.CellId = loc.CellId
                WHERE loc.RackId = @refRackId
                AND loc.xLevel = @refLevel
                AND loc.xColumn BETWEEN @refColumn - 1 AND @refColumn + 1
                AND loc.LocationId <> @refLocationId
                AND loc.xExists = 1
                AND loc.UnitloadCount = 0
                AND loc.OutboundCount = 0
                AND loc.InboundDisabled = 0
                AND loc.InboundCount = 0
                AND loc.WeightLimit >= @weight
                AND loc.HeightLimit >= @height
                AND loc.StorageGroup = @storageGroup
                AND loc.xSpecification = @locSpec
                ORDER BY ABS(loc.xColumn - @refColumn), loc.xColumn",
                new
                {
                    refRackId = referenceLocation.RackId.Value,
                    refLevel = referenceLocation.xLevel,
                    refColumn = referenceLocation.xColumn,
                    refLocationId = referenceLocation.LocationId,
                    weight = storageInfo.Weight,
                    height = storageInfo.Height,
                    storageGroup = storageInfo.StorageGroup,
                    locSpec = storageInfo.ContainerSpecification
                });

            if (nearby != null)
            {
                _logger.LogInformation("[库位分配] 临近命中: 参考={RefLoc}({RefCol},{RefLevel}), 分配={TargetLoc}({TargetCol},{TargetLevel})",
                    referenceLocation.LocationCode, referenceLocation.xColumn, referenceLocation.xLevel,
                    nearby.LocationCode, nearby.xColumn, nearby.xLevel);
                return nearby;
            }

            _logger.LogInformation("[库位分配] 临近未命中（RackId={RackId}, Level={Level}, Column={Column}±1），回退普通分配",
                referenceLocation.RackId, referenceLocation.xLevel, referenceLocation.xColumn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[库位分配] 临近查询异常，回退普通分配");
        }

        return await AllocateAsync(startLocation, unitload, ct);
    }

    /// <summary>
    /// 解析分号分隔的巷道编码
    /// </summary>
    private static string[] ParseLanewayCodes(string? lanewayCodes)
    {
        if (string.IsNullOrWhiteSpace(lanewayCodes))
            return Array.Empty<string>();

        return lanewayCodes
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// 从 Unitload 构建存储信息
    /// </summary>
    private static UnitloadStorageInfo BuildStorageInfo(Unitload unitload)
    {
        return new UnitloadStorageInfo
        {
            Weight = unitload.Weight ?? 0,
            Height = unitload.Height ?? 0,
            StorageGroup = unitload.StorageGroup ?? string.Empty,
            SubStorageGroup = null,
            ContainerSpecification = unitload.ContainerSpecification,
            OutFlag = unitload.OutFlag
        };
    }

    /// <summary>
    /// 验证当前工艺是否匹配位置 Tag（Tag 为分号分隔的工艺列表）
    /// </summary>
    public static bool IsTagMatch(string? tag, string? currentOperation)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        if (string.IsNullOrWhiteSpace(currentOperation))
            return false;

        var tags = tag.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tags.Any(t => string.Equals(t, currentOperation, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 任务归档：将 TransTask 归档到 ArchivedTasks，然后删除原 TransTask
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <param name="transTask">要归档的任务</param>
    /// <param name="actualLocationCode">WCS 实际到达库位编码（可选）</param>
    /// <param name="isCancelled">是否已取消/拒绝</param>
    public static void ArchiveTask(WmsDbContext db, TransTask transTask, string? actualLocationCode = null, bool isCancelled = false)
    {
        var archived = new ArchivedTask
        {
            TaskCode = transTask.TaskCode,
            TaskType = transTask.TaskType,
            CreatedTime = transTask.CreatedTime,
            UnitloadCode = transTask.UnitloadCode ?? transTask.Unitload?.ContainerCode,
            FromLocationCode = transTask.StartLocation?.LocationCode,
            ToLocationCode = transTask.EndLocation?.LocationCode,
            ActualLocationCode = actualLocationCode,
            ForWcs = transTask.ForWcs,
            WasSentToWcs = transTask.WasSentToWcs,
            SentToWcsAt = transTask.SentToWcsAt,
            OrderCode = transTask.OrderCode,
            Comment = transTask.Comment,
            Ext1 = transTask.Ext1,
            Ext2 = transTask.Ext2,
            WareHouse = transTask.WareHouse,
            LocationGroup = transTask.LocationGroup,
            Cancelled = isCancelled,
            Status = isCancelled ? "已取消" : "已完成",
            ArchivedAt = DateTime.Now
        };

        db.ArchivedTasks.Add(archived);
        db.TransTasks.Remove(transTask);
    }

    /// <summary>
    /// 创建库存流水记录
    /// </summary>
    /// <param name="unitload">托盘对象</param>
    /// <param name="transTask">运输任务</param>
    /// <param name="locationId">库位ID</param>
    /// <param name="bizType">业务类型（入库/出库）</param>
    public static Flow CreateFlow(Unitload unitload, TransTask transTask, int locationId, string bizType, int materialId, UnitloadItem? item = null)
    {
        return new Flow
        {
            CreatedTime = DateTime.Now,
            MaterialId = materialId,
            ContainerCode = unitload.ContainerCode,
            UnitloadId = unitload.UnitloadId,
            LocationId = locationId,
            BizType = bizType,
            Direction = bizType,
            TxNo = transTask.TaskCode,
            OrderCode = transTask.OrderCode,
            Comment = transTask.Comment,
            Batch = item?.Batch,
            Uom = item?.Uom,
            Quantity = item?.Quantity,
            StockStatus = item?.StockStatus
        };
    }

    /// <summary>
    /// 添加 UnitloadOp 操作流水记录
    /// </summary>
    public static void AddUnitloadOp(WmsDbContext db, string containerCode, string opType, string direction, string? comment = null, string? createdBy = null)
    {
        var now = DateTime.Now;
        db.UnitloadOps.Add(new UnitloadOp
        {
            ContainerCode = containerCode,
            OpType = opType,
            Direction = direction,
            Comment = comment,
            CreatedBy = createdBy,
            CreatedTime = now,
            ModifiedTime = now
        });
    }

    /// <summary>
    /// Unitload 归档：拷贝到 Archive 表 → 删除原始数据
    /// </summary>
    /// <remarks>
    /// 纯数据操作，不含验证、事务、UnitloadOp。调用方负责外围控制。
    /// </remarks>
    public static void ArchiveUnitload(WmsDbContext db, Unitload unitload, string archiveReason, string comment)
    {
        var now = DateTime.Now;

        // 1. 拷贝 Unitload → ArchivedUnitload
        var archivedUnitload = new ArchivedUnitload
        {
            ContainerCode = unitload.ContainerCode,
            CreatedTime = unitload.CreatedTime,
            CreatedBy = unitload.CreatedBy,
            Weight = unitload.Weight,
            Height = unitload.Height,
            Length = unitload.Length,
            Width = unitload.Width,
            Volume = unitload.Volume,
            StorageGroup = unitload.StorageGroup,
            OutFlag = unitload.OutFlag,
            ContainerSpecification = unitload.ContainerSpecification,
            HasCountingError = unitload.HasCountingError,
            HasMsgError = unitload.HasMsgError,
            LocationId = unitload.LocationId,
            CurrentLocationTime = unitload.CurrentLocationTime,
            OpHintType = unitload.OpHintType,
            OpHintInfo = unitload.OpHintInfo,
            ArchivedAt = now,
            ArchiveReason = archiveReason,
            Comment = comment,
            OperationNumber = unitload.OperationNumber,
            CurrentOperation = unitload.CurrentOperation,
            NextOperation = unitload.NextOperation,
            IsExcludeCurrentUnitload = unitload.IsExcludeCurrentUnitload,
            IsUpload = unitload.IsUpload,
            IsAdvance = unitload.IsAdvance,
            IsSupplement = unitload.IsSupplement,
            IsToHangke = unitload.IsToHangke
        };
        db.ArchivedUnitloads.Add(archivedUnitload);
        db.SaveChanges();

        // 2. 拷贝 UnitloadItems → ArchivedUnitloadItems + ArchivedUnitloadItemDetails
        if (unitload.UnitloadItems != null)
        {
            foreach (var item in unitload.UnitloadItems)
            {
                var archivedItem = new ArchivedUnitloadItem
                {
                    UnitloadId = archivedUnitload.Id,
                    MaterialId = item.MaterialId ?? 0,
                    Batch = item.Batch,
                    StockStatus = item.StockStatus,
                    Quantity = item.Quantity ?? 0,
                    FalseQuantity = item.FalseQuantity,
                    Uom = item.Uom,
                    ProductionTime = item.ProductionTime ?? now,
                    OutOrdering = item.OutOrdering,
                    BoxCode = item.BoxCode,
                    Position = item.Position,
                    xLevel = item.xLevel,
                    OperationNumber = item.OperationNumber ?? 1,
                    BatchNumber = item.BatchNumber,
                    IsAdvance = item.IsAdvance,
                    IsSupplement = item.IsSupplement
                };
                db.ArchivedUnitloadItems.Add(archivedItem);
                db.SaveChanges();

                if (item.UnitloadItemDetails != null)
                {
                    foreach (var detail in item.UnitloadItemDetails)
                    {
                        var archivedDetail = new ArchivedUnitloadItemDetail
                        {
                            UnitloadItemId = archivedItem.Id,
                            BarCode = detail.BarCode,
                            xLevel = detail.xLevel,
                            OCV3 = detail.OCV3,
                            IR3 = detail.IR3,
                            V3KeYa = detail.V3KeYa,
                            OCV4 = detail.OCV4,
                            IR4 = detail.IR4,
                            V4KeYa = detail.V4KeYa,
                            Capacity = detail.Capacity,
                            KVal = detail.KVal,
                            CCP = detail.CCP,
                            Dcirnz = detail.Dcirnz,
                            Sequence = detail.Sequence,
                            Comment = detail.Comment,
                            LocIndex = detail.LocIndex,
                            Status = detail.Status
                        };
                        db.ArchivedUnitloadItemDetails.Add(archivedDetail);
                    }
                }
            }
        }

        db.SaveChanges();

        // 3. 删除原始数据（Details → Items → Unitload）
        var itemIds = unitload.UnitloadItems?.Select(ui => ui.UnitloadItemId).ToList() ?? [];
        if (itemIds.Count > 0)
        {
            db.Set<UnitloadItemDetail>()
                .RemoveRange(db.Set<UnitloadItemDetail>().Where(d => itemIds.Contains(d.UnitloadItemId!.Value)));
        }
        db.Set<UnitloadItem>()
            .RemoveRange(db.Set<UnitloadItem>().Where(i => i.UnitloadId == unitload.UnitloadId));
        db.Set<Unitload>().Remove(unitload);

        db.SaveChanges();
    }

    /// <summary>
    /// Unitload 归档（异步版本）：拷贝到 Archive 表 → 删除原始数据
    /// </summary>
    /// <remarks>
    /// 纯数据操作，不含验证、事务、UnitloadOp。调用方负责外围控制。
    /// </remarks>
    public static async Task ArchiveUnitloadAsync(WmsDbContext db, Unitload unitload, string archiveReason, string comment)
    {
        var now = DateTime.Now;

        // 1. 拷贝 Unitload → ArchivedUnitload
        var archivedUnitload = new ArchivedUnitload
        {
            ContainerCode = unitload.ContainerCode,
            CreatedTime = unitload.CreatedTime,
            CreatedBy = unitload.CreatedBy,
            Weight = unitload.Weight,
            Height = unitload.Height,
            Length = unitload.Length,
            Width = unitload.Width,
            Volume = unitload.Volume,
            StorageGroup = unitload.StorageGroup,
            OutFlag = unitload.OutFlag,
            ContainerSpecification = unitload.ContainerSpecification,
            HasCountingError = unitload.HasCountingError,
            HasMsgError = unitload.HasMsgError,
            LocationId = unitload.LocationId,
            CurrentLocationTime = unitload.CurrentLocationTime,
            OpHintType = unitload.OpHintType,
            OpHintInfo = unitload.OpHintInfo,
            ArchivedAt = now,
            ArchiveReason = archiveReason,
            Comment = comment,
            OperationNumber = unitload.OperationNumber,
            CurrentOperation = unitload.CurrentOperation,
            NextOperation = unitload.NextOperation,
            IsExcludeCurrentUnitload = unitload.IsExcludeCurrentUnitload,
            IsUpload = unitload.IsUpload,
            IsAdvance = unitload.IsAdvance,
            IsSupplement = unitload.IsSupplement,
            IsToHangke = unitload.IsToHangke
        };
        db.ArchivedUnitloads.Add(archivedUnitload);
        await db.SaveChangesAsync();

        // 2. 拷贝 UnitloadItems → ArchivedUnitloadItems + ArchivedUnitloadItemDetails
        if (unitload.UnitloadItems != null)
        {
            foreach (var item in unitload.UnitloadItems)
            {
                var archivedItem = new ArchivedUnitloadItem
                {
                    UnitloadId = archivedUnitload.Id,
                    MaterialId = item.MaterialId ?? 0,
                    Batch = item.Batch,
                    StockStatus = item.StockStatus,
                    Quantity = item.Quantity ?? 0,
                    FalseQuantity = item.FalseQuantity,
                    Uom = item.Uom,
                    ProductionTime = item.ProductionTime ?? now,
                    OutOrdering = item.OutOrdering,
                    BoxCode = item.BoxCode,
                    Position = item.Position,
                    xLevel = item.xLevel,
                    OperationNumber = item.OperationNumber ?? 1,
                    BatchNumber = item.BatchNumber,
                    IsAdvance = item.IsAdvance,
                    IsSupplement = item.IsSupplement
                };
                db.ArchivedUnitloadItems.Add(archivedItem);
                await db.SaveChangesAsync();

                if (item.UnitloadItemDetails != null)
                {
                    foreach (var detail in item.UnitloadItemDetails)
                    {
                        var archivedDetail = new ArchivedUnitloadItemDetail
                        {
                            UnitloadItemId = archivedItem.Id,
                            BarCode = detail.BarCode,
                            xLevel = detail.xLevel,
                            OCV3 = detail.OCV3,
                            IR3 = detail.IR3,
                            V3KeYa = detail.V3KeYa,
                            OCV4 = detail.OCV4,
                            IR4 = detail.IR4,
                            V4KeYa = detail.V4KeYa,
                            Capacity = detail.Capacity,
                            KVal = detail.KVal,
                            CCP = detail.CCP,
                            Dcirnz = detail.Dcirnz,
                            Sequence = detail.Sequence,
                            Comment = detail.Comment,
                            LocIndex = detail.LocIndex,
                            Status = detail.Status
                        };
                        db.ArchivedUnitloadItemDetails.Add(archivedDetail);
                    }
                }
            }
        }

        await db.SaveChangesAsync();

        // 3. 删除原始数据（Details → Items → Unitload）
        var itemIds = unitload.UnitloadItems?.Select(ui => ui.UnitloadItemId).ToList() ?? [];
        if (itemIds.Count > 0)
        {
            db.Set<UnitloadItemDetail>()
                .RemoveRange(db.Set<UnitloadItemDetail>().Where(d => itemIds.Contains(d.UnitloadItemId!.Value)));
        }
        db.Set<UnitloadItem>()
            .RemoveRange(db.Set<UnitloadItem>().Where(i => i.UnitloadId == unitload.UnitloadId));
        db.Set<Unitload>().Remove(unitload);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// 归档 Unitload（使用 Items 快照副本，用于叠盘场景）
    /// 叠盘时 source 的 Items 已转移到 target，导航属性已清空，需通过快照副本归档
    /// </summary>
    public static async Task ArchiveUnitloadAsync(WmsDbContext db, Unitload unitload, string archiveReason, string comment,
        List<dynamic> itemsSnapshot)
    {
        var now = DateTime.Now;

        // 1. 拷贝 Unitload → ArchivedUnitload
        var archivedUnitload = new ArchivedUnitload
        {
            ContainerCode = unitload.ContainerCode,
            CreatedTime = unitload.CreatedTime,
            CreatedBy = unitload.CreatedBy,
            Weight = unitload.Weight,
            Height = unitload.Height,
            Length = unitload.Length,
            Width = unitload.Width,
            Volume = unitload.Volume,
            StorageGroup = unitload.StorageGroup,
            OutFlag = unitload.OutFlag,
            ContainerSpecification = unitload.ContainerSpecification,
            HasCountingError = unitload.HasCountingError,
            HasMsgError = unitload.HasMsgError,
            LocationId = unitload.LocationId,
            CurrentLocationTime = unitload.CurrentLocationTime,
            OpHintType = unitload.OpHintType,
            OpHintInfo = unitload.OpHintInfo,
            ArchivedAt = now,
            ArchiveReason = archiveReason,
            Comment = comment,
            OperationNumber = unitload.OperationNumber,
            CurrentOperation = unitload.CurrentOperation,
            NextOperation = unitload.NextOperation,
            IsExcludeCurrentUnitload = unitload.IsExcludeCurrentUnitload,
            IsUpload = unitload.IsUpload,
            IsAdvance = unitload.IsAdvance,
            IsSupplement = unitload.IsSupplement,
            IsToHangke = unitload.IsToHangke
        };
        db.ArchivedUnitloads.Add(archivedUnitload);
        await db.SaveChangesAsync();

        // 2. 从快照副本拷贝 Items + Details
        if (itemsSnapshot != null)
        {
            foreach (dynamic snap in itemsSnapshot)
            {
                var archivedItem = new ArchivedUnitloadItem
                {
                    UnitloadId = archivedUnitload.Id,
                    MaterialId = (int)(snap.MaterialId ?? 0),
                    Batch = (string?)snap.Batch,
                    StockStatus = (string?)snap.StockStatus,
                    Quantity = Convert.ToDecimal(snap.Quantity ?? 0),
                    FalseQuantity = snap.FalseQuantity != null ? Convert.ToDecimal(snap.FalseQuantity) : null,
                    Uom = (string?)snap.Uom,
                    ProductionTime = (DateTime?)snap.ProductionTime ?? now,
                    OutOrdering = (string?)snap.OutOrdering,
                    BoxCode = (string?)snap.BoxCode,
                    Position = snap.Position != null ? Convert.ToInt32(snap.Position) : null,
                    xLevel = (string?)snap.xLevel,
                    OperationNumber = (int)(snap.OperationNumber ?? 1),
                    BatchNumber = snap.BatchNumber != null ? Convert.ToInt32(snap.BatchNumber) : null,
                    IsAdvance = snap.IsAdvance != null ? Convert.ToInt32(snap.IsAdvance) : null,
                    IsSupplement = snap.IsSupplement != null ? Convert.ToInt32(snap.IsSupplement) : null
                };
                db.ArchivedUnitloadItems.Add(archivedItem);
                await db.SaveChangesAsync();

                if (snap.Details is IList<dynamic> details)
                {
                    foreach (dynamic d in details)
                    {
                        db.ArchivedUnitloadItemDetails.Add(new ArchivedUnitloadItemDetail
                        {
                            UnitloadItemId = archivedItem.Id,
                            BarCode = (string?)d.BarCode,
                            xLevel = (string?)d.xLevel,
                            OCV3 = d.OCV3 != null ? Convert.ToDecimal(d.OCV3) : null,
                            IR3 = d.IR3 != null ? Convert.ToDecimal(d.IR3) : null,
                            V3KeYa = d.V3KeYa != null ? Convert.ToDecimal(d.V3KeYa) : null,
                            OCV4 = d.OCV4 != null ? Convert.ToDecimal(d.OCV4) : null,
                            IR4 = d.IR4 != null ? Convert.ToDecimal(d.IR4) : null,
                            V4KeYa = d.V4KeYa != null ? Convert.ToDecimal(d.V4KeYa) : null,
                            Capacity = d.Capacity != null ? Convert.ToDecimal(d.Capacity) : null,
                            KVal = d.KVal != null ? Convert.ToDecimal(d.KVal) : null,
                            CCP = d.CCP != null ? Convert.ToDecimal(d.CCP) : null,
                            Dcirnz = d.Dcirnz != null ? Convert.ToDecimal(d.Dcirnz) : null,
                            Sequence = (string?)d.Sequence,
                            Comment = (string?)d.Comment,
                            LocIndex = d.LocIndex != null ? Convert.ToInt32(d.LocIndex) : null,
                            Status = (string?)d.Status
                        });
                    }
                }
            }
        }

        await db.SaveChangesAsync();

        // 3. 删除原始数据（source 的 Items 已转移到 target，只需删除 Unitload 本身）
        db.Set<Unitload>().Remove(unitload);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// 清理 Unitload 中的空托盘 Item，若 Unitload 变空则归档+删除整个 Unitload
    /// </summary>
    /// <param name="db">数据库上下文</param>
    /// <param name="unitload">要清理的托盘（需已加载 UnitloadItems + Material 导航属性）</param>
    /// <param name="archiveReason">归档原因（记录到 ArchivedUnitload.ArchiveReason）</param>
    /// <returns>true 表示 Unitload 已被删除；false 表示仍存活</returns>
    public static async Task<bool> CleanupEmptyTrayItemsAsync(WmsDbContext db, Unitload unitload, string archiveReason)
    {
        if (unitload?.UnitloadItems == null || unitload.UnitloadItems.Count == 0)
            return false;

        // 1. 判断是否所有 Item 都是空托盘
        bool allEmpty = unitload.UnitloadItems.All(ui => ui.Material?.MaterialCode == CommonTypes.空托盘);

        if (allEmpty)
        {
            // ===== 纯空托盘 Unitload → 归档 + 删除整个 Unitload =====
            var uid = unitload.UnitloadId;
            var containerCode = unitload.ContainerCode ?? string.Empty;

            // 1a. 清理 FK 引用（ArchiveUnitloadAsync 不含此步骤，必须先做）
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE Flows SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE Stocks SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);
            // TransTasks 必须同时保留 UnitloadCode 快照（ArchiveTask 归档时读取此字段）
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE TransTasks SET UnitloadId = NULL, UnitloadCode = {1} WHERE UnitloadId = {0}",
                uid, containerCode);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE UnionUnitloadItems SET UnitloadId = NULL WHERE UnitloadId = {0}", uid);

            // 1b. 归档 + 删除（复用现有方法）
            await ArchiveUnitloadAsync(db, unitload, archiveReason, $"空托盘清理 ContainerCode={containerCode}");

            // 1c. 清理内存导航集合（ArchiveUnitloadAsync 不做此操作）
            // 实体已 Detached，但 unitload.UnitloadItems 仍引用旧 Item 对象
            // 必须清空，否则下游 SplitUnitloadHandler 的 Count > 1 检查会误判（合并场景 2+ 空托盘）
            unitload.UnitloadItems?.Clear();

            return true;
        }
        else
        {
            // ===== 混合托盘 → 只删除空托盘 Item，Unitload 存活 =====
            var emptyTrayItems = unitload.UnitloadItems
                .Where(ui => ui.Material?.MaterialCode == CommonTypes.空托盘)
                .ToList();

            if (emptyTrayItems.Count == 0)
                return false;

            var emptyItemIds = emptyTrayItems.Select(ui => ui.UnitloadItemId).ToHashSet();

            // 删除空托盘 Item 的 Details（防御性：空托盘通常无 details）
            db.Set<UnitloadItemDetail>().RemoveRange(
                db.Set<UnitloadItemDetail>().Where(d => emptyItemIds.Contains(d.UnitloadItemId!.Value)));

            // 标记 Item 为 Deleted
            db.Set<UnitloadItem>().RemoveRange(emptyTrayItems);

            // 从导航集合移除（确保下游 unitload.UnitloadItems.Count 正确）
            foreach (var item in emptyTrayItems)
                unitload.UnitloadItems.Remove(item);

            return false;
        }
    }

    /// <summary>
    /// 拆盘：将多 Item 的 Unitload 拆分为多个独立 Unitload（每个 Unitload 含一个 UnitloadItem），
    /// 归档原 Unitload
    /// </summary>
    /// <remarks>异步方法，调用方需 await</remarks>
    public static async Task SplitUnitloadAsync(WmsDbContext db, Unitload unitload, int targetLocationId)
    {
        var now = DateTime.Now;
        var sourceUnitloadId = unitload.UnitloadId;
        var sourceContainerCode = unitload.ContainerCode;

        // 1. 保存 items 数据到内存（归档后原始数据将被删除）
        var itemsData = unitload.UnitloadItems!
            .Select(item => new
            {
                item.MaterialId, item.Batch, item.StockStatus, item.Quantity, item.FalseQuantity,
                item.Uom, item.ProductionTime, item.OutOrdering, item.BoxCode, item.Position,
                item.xLevel, item.OperationNumber, item.BatchNumber, item.IsAdvance, item.IsSupplement,
                Details = (item.UnitloadItemDetails ?? Enumerable.Empty<UnitloadItemDetail>())
                    .Select(d => new
                    {
                        d.BarCode, d.xLevel, d.OCV3, d.IR3, d.V3KeYa, d.OCV4, d.IR4, d.V4KeYa,
                        d.Capacity, d.KVal, d.CCP, d.Dcirnz, d.Sequence, d.Comment, d.LocIndex, d.Status
                    }).ToList()
            }).ToList();

        // 2. 清除 FK 引用（DB 级 FK 约束）
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Flows SET UnitloadId = NULL WHERE UnitloadId = {0}", sourceUnitloadId);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Stocks SET UnitloadId = NULL WHERE UnitloadId = {0}", sourceUnitloadId);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE TransTasks SET UnitloadId = NULL, UnitloadCode = {1} WHERE UnitloadId = {0}",
            sourceUnitloadId, sourceContainerCode);

        // 3. 归档原 Unitload（完整流程：归档 Unitload + Items + Details → 删除全部）
        await ArchiveUnitloadAsync(db, unitload, "SIM拆盘", $"ContainerCode={sourceContainerCode}");

        // 4. 用内存数据创建 N 个新 Unitload（每个含一个 Item，原 ContainerCode 已释放）
        for (int i = 0; i < itemsData.Count; i++)
        {
            var itemData = itemsData[i];
            var newUnitload = new Unitload
            {
                ContainerCode = itemData.BoxCode,
                Version = unitload.Version ?? 0,
                Weight = unitload.Weight,
                Height = unitload.Height,
                Length = unitload.Length,
                Width = unitload.Width,
                Volume = unitload.Volume,
                StorageGroup = unitload.StorageGroup,
                OutFlag = unitload.OutFlag,
                ContainerSpecification = unitload.ContainerSpecification,
                LocationId = targetLocationId,
                CurrentLocationTime = now,
                OperationNumber = unitload.OperationNumber,
                CurrentOperation = unitload.CurrentOperation,
                NextOperation = unitload.NextOperation,
                IsAdvance = unitload.IsAdvance,
                IsSupplement = unitload.IsSupplement,
                IsToHangke = unitload.IsToHangke,
                CreatedTime = now,
                ModifiedTime = now
            };

            var newItem = new UnitloadItem
            {
                MaterialId = itemData.MaterialId,
                Batch = itemData.Batch,
                StockStatus = itemData.StockStatus,
                Quantity = itemData.Quantity,
                FalseQuantity = itemData.FalseQuantity,
                Uom = itemData.Uom,
                ProductionTime = itemData.ProductionTime,
                OutOrdering = itemData.OutOrdering,
                BoxCode = itemData.BoxCode,
                Position = itemData.Position,
                xLevel = itemData.xLevel,
                OperationNumber = itemData.OperationNumber,
                BatchNumber = itemData.BatchNumber,
                IsAdvance = itemData.IsAdvance,
                IsSupplement = itemData.IsSupplement,
                Unitload = newUnitload
            };

            db.Unitloads.Add(newUnitload);
            db.UnitloadItems.Add(newItem);

            foreach (var detail in itemData.Details)
            {
                db.UnitloadItemDetails.Add(new UnitloadItemDetail
                {
                    BarCode = detail.BarCode,
                    xLevel = detail.xLevel,
                    OCV3 = detail.OCV3,
                    IR3 = detail.IR3,
                    V3KeYa = detail.V3KeYa,
                    OCV4 = detail.OCV4,
                    IR4 = detail.IR4,
                    V4KeYa = detail.V4KeYa,
                    Capacity = detail.Capacity,
                    KVal = detail.KVal,
                    CCP = detail.CCP,
                    Dcirnz = detail.Dcirnz,
                    Sequence = detail.Sequence,
                    Comment = detail.Comment,
                    LocIndex = detail.LocIndex,
                    Status = detail.Status,
                    UnitloadItem = newItem
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// 叠盘：将 source Unitload 的所有 Items 和 Details 合并到 target Unitload，归档 source
    /// </summary>
    /// <remarks>
    /// 异步方法，调用方需 await。
    /// 仅执行数据合并和归档，不含验证、事务、UnitloadOp。调用方负责外围控制。
    /// </remarks>
    public static async Task MergeUnitloadsAsync(WmsDbContext db, Unitload targetUnitload, Unitload sourceUnitload, string archiveReason = "叠盘")
    {
        var sourceUnitloadId = sourceUnitload.UnitloadId;

        // 1. 转移 source 的所有 UnitloadItems 到 target（仅修改 UnitloadId）
        var itemsList = sourceUnitload.UnitloadItems?.ToList() ?? [];
        foreach (var item in itemsList)
        {
            item.UnitloadId = targetUnitload.UnitloadId;
        }
        await db.SaveChangesAsync();

        // 1.5 保存 Items 副本（含 Details），供归档快照使用
        List<dynamic> itemsSnapshot = itemsList.Select(item => (dynamic)new
        {
            item.UnitloadItemId,
            item.MaterialId,
            item.Batch,
            item.StockStatus,
            item.Quantity,
            item.FalseQuantity,
            item.Uom,
            item.ProductionTime,
            item.OutOrdering,
            item.BoxCode,
            item.Position,
            item.xLevel,
            item.OperationNumber,
            item.BatchNumber,
            item.IsAdvance,
            item.IsSupplement,
            Details = item.UnitloadItemDetails?.Select(d => new
            {
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
                d.Comment,
                d.LocIndex,
                d.Status
            }).ToList()
        }).Cast<dynamic>().ToList();

        // 2. 清空 source 的导航属性，防止 ArchiveUnitload 重复归档已转移的 Items
        sourceUnitload.UnitloadItems = null;

        // 3. 清除数据库外键引用（Flows、Stocks、TransTasks 可能有 DB 级 FK 约束）
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Flows SET UnitloadId = NULL WHERE UnitloadId = {0}", sourceUnitloadId);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Stocks SET UnitloadId = NULL WHERE UnitloadId = {0}", sourceUnitloadId);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE TransTasks SET UnitloadId = NULL, UnitloadCode = {1} WHERE UnitloadId = {0}",
            sourceUnitloadId, sourceUnitload.ContainerCode);

        // 4. 归档 source Unitload（用 Items 副本归档快照）
        await ArchiveUnitloadAsync(db, sourceUnitload, archiveReason,
            $"MergeInto={targetUnitload.ContainerCode}", itemsSnapshot);
    }
}

/// <summary>
/// 任务编码生成器 — yyMMdd + 6位流水号（基于 AppSeqs 数据库序列，原子操作）
/// </summary>
public static class TaskCodeGenerator
{
    /// <summary>
    /// 生成任务编码
    /// </summary>
    /// <remarks>
    /// 使用 SQL Server OUTPUT 子句原子自增，并发安全。
    /// SeqName 格式：TaskCode_260604，每天自动创建新记录。
    /// </remarks>
    public static async Task<string> GenerateAsync(WmsDbContext db)
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var seqName = $"TaskCode_{today}";

        // 原子自增（单条 SQL，并发安全）
        var results = await db.Database
            .SqlQueryRaw<int>(
                "UPDATE AppSeqs SET NextVal = NextVal + 1 OUTPUT INSERTED.NextVal WHERE SeqName = {0}",
                seqName)
            .ToListAsync();

        if (results.Count > 0)
        {
            return $"{today}{results[0]:D6}";
        }

        // 记录不存在，创建（NextVal=2 表示第一个已用，下次返回 2）
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO AppSeqs (SeqName, NextVal, Increment, MinValue, MaxValue, Cycle) VALUES ({0}, 2, 1, 1, 999999, 0)",
            seqName);

        return $"{today}000001";
    }
}
