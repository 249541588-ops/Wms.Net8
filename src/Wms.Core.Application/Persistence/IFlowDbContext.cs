using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Flow;
using Wms.Core.Domain.Entities.StockFlow;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Entities.Archive;

namespace Wms.Core.Application.Persistence;

/// <summary>
/// FlowContext 和节点处理器实际需要的 DbContext 子集。
/// WmsDbContext 实现此接口（DbSet 同名隐式实现）。
/// 清单来源：grep 25 个节点的 context.Db. 调用。
/// </summary>
public interface IFlowDbContext
{
    DbSet<Location> Locations { get; }
    DbSet<Unitload> Unitloads { get; }
    DbSet<UnitloadItem> UnitloadItems { get; }
    DbSet<UnitloadItemDetail> UnitloadItemDetails { get; }
    DbSet<TransTask> TransTasks { get; }
    DbSet<Flow> Flows { get; }
    DbSet<FlowInstance> FlowInstances { get; }
    DbSet<FlowNodeLog> FlowNodeLogs { get; }
    DbSet<FlowTemplate> FlowTemplates { get; }
    DbSet<FlowNode> FlowNodes { get; }

    // 库位分配 / 归档相关（LocationAllocator 等辅助类使用）
    DbSet<Rack> Racks { get; }
    DbSet<Laneway> Laneways { get; }
    DbSet<UnitloadOp> UnitloadOps { get; }
    DbSet<ArchivedTask> ArchivedTasks { get; }
    DbSet<ArchivedUnitload> ArchivedUnitloads { get; }
    DbSet<ArchivedUnitloadItem> ArchivedUnitloadItems { get; }
    DbSet<ArchivedUnitloadItemDetail> ArchivedUnitloadItemDetails { get; }

    /// <summary>
    /// 兜底：节点访问未在接口中显式列出的 DbSet（如 Set&lt;WasteBatchSetting&gt;、Set&lt;BasicDictionary&gt;）
    /// </summary>
    DbSet<T> Set<T>() where T : class;

    /// <summary>
    /// 用于 Database.BeginTransactionAsync / ExecuteSqlRawAsync 等
    /// </summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// 用于节点中 SaveChangesAsync 调用
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步保存（LocationAllocator 的同步方法使用；DbContext 原生支持同步/异步两种）
    /// </summary>
    int SaveChanges();

    /// <summary>
    /// 用于节点中 Entry(entity).Reference(...).LoadAsync() 显式加载（NotifyHangKeHandler 等）
    /// </summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
