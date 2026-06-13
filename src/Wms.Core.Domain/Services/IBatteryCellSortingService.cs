using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Requests;

namespace Wms.Core.Domain.Services;

/// <summary>
/// 电芯分选服务接口
/// </summary>
public interface IBatteryCellSortingService
{
    /// <summary>
    /// 分页查询
    /// </summary>
    (IEnumerable<BatteryCellSorting> Data, int TotalCount) GetPagedList(string? keyword, int pageNumber, int pageSize);

    /// <summary>
    /// 根据ID获取
    /// </summary>
    BatteryCellSorting? GetById(int id);

    /// <summary>
    /// 创建
    /// </summary>
    BatteryCellSorting Create(BatteryCellSortingRequest request);

    /// <summary>
    /// 更新
    /// </summary>
    BatteryCellSorting? Update(int id, BatteryCellSortingRequest request);

    /// <summary>
    /// 删除
    /// </summary>
    bool Delete(int id);
}
