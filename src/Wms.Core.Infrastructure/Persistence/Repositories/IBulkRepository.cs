using global::System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// 批量操作仓储接口（Infrastructure 层专用，使用 EF Core 的 SetPropertyCalls 类型）
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IBulkRepository<T, TKey> where T : class, IEntity<TKey>
{
    /// <summary>
    /// 批量更新（直接在数据库执行，绕过 Change Tracker，不触发审计字段自动填充）
    /// </summary>
    /// <param name="predicate">筛选条件</param>
    /// <param name="setters">要更新的属性设置委托</param>
    /// <returns>受影响的行数</returns>
    Task<int> BulkUpdateAsync(
        Expression<Func<T, bool>> predicate,
        Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setters);

    /// <summary>
    /// 批量删除（直接在数据库执行，绕过 Change Tracker，不触发审计字段自动填充）
    /// </summary>
    /// <param name="predicate">筛选条件</param>
    /// <returns>受影响的行数</returns>
    Task<int> BulkDeleteAsync(Expression<Func<T, bool>> predicate);
}
