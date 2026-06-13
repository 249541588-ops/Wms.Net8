using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Engine;

/// <summary>
/// 流程上下文 — 节点间共享数据
/// </summary>
public class FlowContext
{
    /// <summary>
    /// 共享 DbContext（所有节点操作同一被 EF Core 跟踪的实体实例）
    /// </summary>
    public WmsDbContext Db { get; }

    /// <summary>
    /// 原始 WCS 请求（请求阶段）
    /// </summary>
    public WcsRequest? WcsRequest { get; set; }

    /// <summary>
    /// 原始 WCS 任务（完成阶段）
    /// </summary>
    public WcsTask? WcsTask { get; set; }

    /// <summary>
    /// 起始位置（请求来源位置）
    /// </summary>
    public Location? StartLocation { get; set; }

    /// <summary>
    /// 目标位置（分配/搬运目标位置）
    /// </summary>
    public Location? TargetLocation { get; set; }

    /// <summary>
    /// 托盘实体（EF Core 跟踪）
    /// </summary>
    public Unitload? Unitload { get; set; }

    /// <summary>
    /// 运输任务（EF Core 跟踪）
    /// </summary>
    public TransTask? TransTask { get; set; }

    /// <summary>
    /// 当前遍历的容器码（foreach 循环内传递）
    /// </summary>
    public string? CurrentContainerCode { get; set; }

    /// <summary>
    /// 通用数据字典（节点间传递任意数据）
    /// </summary>
    public Dictionary<string, object?> Data { get; } = new();

    /// <summary>
    /// 流程阶段："Request"（请求阶段）/ "Completion"（完成阶段）
    /// </summary>
    public string? Phase { get; set; }

    /// <summary>
    /// 任务是否被取消/拒绝（完成阶段：cancelled/refused 时为 true）
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// 业务类型
    /// </summary>
    public string? BusinessType { get; set; }

    /// <summary>
    /// 业务 ID
    /// </summary>
    public string? BusinessId { get; set; }

    /// <summary>
    /// 当前操作用户
    /// </summary>
    public string? CurrentUser { get; set; }

    public FlowContext(WmsDbContext db)
    {
        Db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 合并节点输出数据到 Data 字典
    /// </summary>
    public void Merge(Dictionary<string, object?>? output)
    {
        if (output == null) return;
        foreach (var kvp in output)
        {
            Data[kvp.Key] = kvp.Value;
        }
    }
}
