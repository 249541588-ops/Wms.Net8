namespace Wms.Core.Domain.Abstractions;

/// <summary>
/// 工作单元接口：统一事务管理抽象，让 FlowContext 和节点处理器不再依赖 WmsDbContext 具体类型。
/// WmsDbContext 实现此接口（显式或隐式）。
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
