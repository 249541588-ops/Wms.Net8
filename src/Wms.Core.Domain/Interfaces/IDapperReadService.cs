using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Entities.Warehouse;

namespace Wms.Core.Domain.Interfaces;

/// <summary>
/// Dapper 高频读取服务接口（只读，适用于条码扫描、库存查询等高频场景）
/// </summary>
public interface IDapperReadService
{
    /// <summary>
    /// 根据条码查询电芯信息
    /// </summary>
    BatteryCell? GetBatteryCellByBarcode(string barcode);

    /// <summary>
    /// 根据库位ID查询库存列表
    /// </summary>
    IEnumerable<Stock> GetStocksByLocation(int locationId);

    /// <summary>
    /// 根据物料ID和库位ID精确查询库存
    /// </summary>
    Stock? GetStockByMaterialAndLocation(int materialId, int locationId);

    /// <summary>
    /// 根据库位编码查询库位信息
    /// </summary>
    Location? GetLocationByCode(string locationCode);

    /// <summary>
    /// 根据批次号查询库存列表
    /// </summary>
    IEnumerable<Stock> GetStocksByBatch(string batch);

    /// <summary>
    /// 根据容器编码查询托盘信息
    /// </summary>
    Unitload? GetUnitloadByCode(string containerCode);
}
