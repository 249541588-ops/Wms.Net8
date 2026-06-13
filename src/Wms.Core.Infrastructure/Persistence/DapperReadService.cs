using Dapper;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Infrastructure.Persistence;

/// <summary>
/// Dapper 高频读取服务实现
/// </summary>
/// <remarks>
/// 使用 Dapper 进行高性能只读查询，适用于条码扫描、库存查询等高频场景。
/// 写入操作仍走 EF Core Repository，保持变更跟踪能力。
/// </remarks>
public class DapperReadService : IDapperReadService
{
    private readonly WmsDbContext _db;

    /// <summary>
    /// 初始化 DapperReadService
    /// </summary>
    /// <param name="db">EF Core 数据库上下文（复用其连接池）</param>
    public DapperReadService(WmsDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 根据条码查询电芯信息
    /// </summary>
    public BatteryCell? GetBatteryCellByBarcode(string barcode)
    {
        var conn = _db.Database.GetDbConnection();
        return conn.QueryFirstOrDefault<BatteryCell>(
            "SELECT * FROM BatteryCells WHERE BarCode = @barcode",
            new { barcode });
    }

    /// <summary>
    /// 根据库位ID查询库存列表
    /// </summary>
    public IEnumerable<Stock> GetStocksByLocation(int locationId)
    {
        var conn = _db.Database.GetDbConnection();
        return conn.Query<Stock>(
            "SELECT * FROM Stocks WHERE LocationId = @locationId",
            new { locationId });
    }

    /// <summary>
    /// 根据物料ID和库位ID精确查询库存
    /// </summary>
    public Stock? GetStockByMaterialAndLocation(int materialId, int locationId)
    {
        var conn = _db.Database.GetDbConnection();
        return conn.QueryFirstOrDefault<Stock>(
            "SELECT * FROM Stocks WHERE MaterialId = @materialId AND LocationId = @locationId",
            new { materialId, locationId });
    }

    /// <summary>
    /// 根据库位编码查询库位信息
    /// </summary>
    public Location? GetLocationByCode(string locationCode)
    {
        var conn = _db.Database.GetDbConnection();
        return conn.QueryFirstOrDefault<Location>(
            "SELECT * FROM Locations WHERE LocationCode = @locationCode",
            new { locationCode });
    }

    /// <summary>
    /// 根据批次号查询库存列表
    /// </summary>
    public IEnumerable<Stock> GetStocksByBatch(string batch)
    {
        var conn = _db.Database.GetDbConnection();
        return conn.Query<Stock>(
            "SELECT * FROM Stocks WHERE Batch = @batch",
            new { batch });
    }

    /// <summary>
    /// 根据容器编码查询托盘信息
    /// </summary>
    public Unitload? GetUnitloadByCode(string containerCode)
    {
        var conn = _db.Database.GetDbConnection();
        return conn.QueryFirstOrDefault<Unitload>(
            "SELECT * FROM Unitloads WHERE ContainerCode = @containerCode",
            new { containerCode });
    }
}
