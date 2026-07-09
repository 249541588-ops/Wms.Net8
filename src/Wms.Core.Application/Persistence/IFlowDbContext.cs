using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Flow;
using Wms.Core.Domain.Entities.StockFlow;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;

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
    DbSet<TransTask> TransTasks { get; }
    DbSet<Flow> Flows { get; }
    DbSet<FlowInstance> FlowInstances { get; }
    DbSet<FlowNodeLog> FlowNodeLogs { get; }
    DbSet<FlowTemplate> FlowTemplates { get; }
    DbSet<FlowNode> FlowNodes { get; }

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
    /// 用于节点中 Entry(entity).Reference(...).LoadAsync() 显式加载（NotifyHangKeHandler 等）
    /// </summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
