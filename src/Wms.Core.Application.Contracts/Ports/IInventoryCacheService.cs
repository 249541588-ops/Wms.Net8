namespace Wms.Core.Application.Ports;

/// <summary>
/// 库存缓存服务接口（Cache-Aside 模式）
/// </summary>
public interface IInventoryCacheService
{
    /// <summary>
    /// 获取库存数量
    /// </summary>
    /// <param name="locationId">库位ID</param>
    /// <param name="materialId">物料ID</param>
    /// <returns>库存数量，null 表示缓存未命中</returns>
    Task<decimal?> GetStockQtyAsync(int locationId, int materialId);

    /// <summary>
    /// 设置库存数量到缓存
    /// </summary>
    Task SetStockQtyAsync(int locationId, int materialId, decimal qty, TimeSpan? expiration = null);

    /// <summary>
    /// 删除库存缓存
    /// </summary>
    Task RemoveStockAsync(int locationId, int materialId);

    /// <summary>
    /// 按库位删除所有库存缓存
    /// </summary>
    Task RemoveByLocationAsync(int locationId);
}
