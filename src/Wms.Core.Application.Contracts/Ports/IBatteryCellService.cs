using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Requests;

namespace Wms.Core.Application.Ports;

/// <summary>
/// 电芯服务接口
/// </summary>
public interface IBatteryCellService
{
    /// <summary>
    /// 分页查询
    /// </summary>
    /// <param name="keyword">关键字（条码/批次/序列号模糊匹配）</param>
    /// <param name="batch">批次（模糊匹配）</param>
    /// <param name="xLevel">档位/X等级（模糊匹配）</param>
    /// <param name="containerCode">容器编码（模糊匹配）</param>
    /// <param name="status">状态（模糊匹配）</param>
    /// <param name="materialId">物料ID（精确匹配）</param>
    /// <param name="pageNumber">页码</param>
    /// <param name="pageSize">每页大小</param>
    (IEnumerable<BatteryCell> Data, int TotalCount) GetPagedList(string? keyword, string? batch, string? xLevel, string? containerCode, string? status, int? materialId, int pageNumber, int pageSize);

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
