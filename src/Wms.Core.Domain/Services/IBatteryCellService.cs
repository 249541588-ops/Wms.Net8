using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Requests;

namespace Wms.Core.Domain.Services;

/// <summary>
/// 电芯服务接口
/// </summary>
public interface IBatteryCellService
{
    /// <summary>
    /// 分页查询
    /// </summary>
    (IEnumerable<BatteryCell> Data, int TotalCount) GetPagedList(string? keyword, int pageNumber, int pageSize);

    /// <summary>
    /// 根据ID获取
    /// </summary>
    BatteryCell? GetById(int id);

    /// <summary>
    /// 创建
    /// </summary>
    BatteryCell Create(BatteryCellRequest request);

    /// <summary>
    /// 更新
    /// </summary>
    BatteryCell? Update(int id, BatteryCellRequest request);

    /// <summary>
    /// 删除
    /// </summary>
    bool Delete(int id);
}
