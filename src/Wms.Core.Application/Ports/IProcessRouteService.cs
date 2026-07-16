using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.ProcessRoute;
using Wms.Core.Domain.ValueObjects;

namespace Wms.Core.Application.Ports;

/// <summary>
/// 工艺路线服务接口
/// </summary>
public interface IProcessRouteService
{
    /// <summary>
    /// 根据物料ID匹配工艺路线（带缓存）
    /// </summary>
    Task<ProcessRouteVersion?> MatchRouteByMaterialAsync(int materialId);

    /// <summary>
    /// 获取路线版本的完整图结构（带缓存）
    /// </summary>
    Task<ProcessRouteGraph?> GetRouteGraphAsync(int versionId);

    /// <summary>
    /// 托盘创建时绑定路线
    /// </summary>
    Task BindRouteAsync(Unitload unitload, int materialId);

    /// <summary>
    /// 推进托盘工序（在 AdvanceOperationHandler 中调用）
    /// 返回 false 表示无路线或路线数据异常，需回退硬编码
    /// </summary>
    Task<bool> AdvanceOperationAsync(Unitload unitload, string? operatorName);

    /// <summary>
    /// 获取托盘在路线中的下一步选项（分支节点返回多个）
    /// </summary>
    Task<List<BranchOptionDto>> GetNextStepOptionsAsync(int unitloadId);

    /// <summary>
    /// 人工选择分支下一步
    /// </summary>
    Task<Result> SelectBranchAsync(int unitloadId, int transitionId, string? operatorName);

    /// <summary>
    /// 获取托盘工艺轨迹（分页，按创建时间倒序，返回全部历史记录含历史轮次）
    /// </summary>
    Task<PagedResult<UnitloadProcessRouteLog>> GetUnitloadTrackAsync(int unitloadId, int pageNumber, int pageSize);

    /// <summary>
    /// 清除缓存
    /// </summary>
    void ClearCache();
}
