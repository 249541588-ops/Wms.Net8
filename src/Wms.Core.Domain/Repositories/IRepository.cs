using global::System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Repositories;

/// <summary>
/// 通用仓储接口
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IRepository<T, TKey> where T : IEntity<TKey>
{
    /// <summary>
    /// 根据ID获取实体
    /// </summary>
    T? GetById(TKey id);

    /// <summary>
    /// 根据ID列表获取实体
    /// </summary>
    IEnumerable<T> GetByIds(IEnumerable<TKey> ids);

    /// <summary>
    /// 获取所有实体
    /// </summary>
    IQueryable<T> GetAll();

    /// <summary>
    /// 根据条件查找实体
    /// </summary>
    IQueryable<T> Find(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// 根据条件查询单个实体
    /// </summary>
    T? FirstOrDefault(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// 添加实体
    /// </summary>
    T Add(T entity);

    /// <summary>
    /// 批量添加实体
    /// </summary>
    IEnumerable<T> AddRange(IEnumerable<T> entities);

    /// <summary>
    /// 更新实体
    /// </summary>
    void Update(T entity);

    /// <summary>
    /// 删除实体
    /// </summary>
    void Delete(T entity);

    /// <summary>
    /// 根据ID删除实体
    /// </summary>
    void Delete(TKey id);

    /// <summary>
    /// 批量删除实体
    /// </summary>
    void DeleteRange(IEnumerable<T> entities);

    /// <summary>
    /// 统计实体数量
    /// </summary>
    int Count();

    /// <summary>
    /// 根据条件统计实体数量
    /// </summary>
    int Count(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// 检查实体是否存在
    /// </summary>
    bool Exists(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// 使用原生 SQL 查询（带参数，防止 SQL 注入）
    /// </summary>
    /// <param name="sql">SQL 查询语句</param>
    /// <param name="parameters">查询参数</param>
    /// <returns>查询结果列表</returns>
    IEnumerable<T> ExecuteSql(string sql, Dictionary<string, object>? parameters = null);

    /// <summary>
    /// 使用原生 SQL 查询并分页（带参数，防止 SQL 注入）
    /// </summary>
    /// <param name="sql">SQL 查询语句（不需要包含 ORDER BY、OFFSET、FETCH）</param>
    /// <param name="countSql">统计总数的 SQL 语句（例如：SELECT COUNT(*) FROM ... WHERE ...）</param>
    /// <param name="parameters">查询参数</param>
    /// <param name="pageNumber">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="orderBy">排序字段（例如：Id DESC, Name ASC）</param>
    /// <returns>分页结果</returns>
    (IEnumerable<T> Data, int TotalCount, int TotalPages) ExecuteSqlPaged(
        string sql,
        string countSql,
        Dictionary<string, object>? parameters,
        int pageNumber,
        int pageSize,
        string orderBy = "Id ASC");

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

/// <summary>
/// 整数主键的仓储接口
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public interface IRepository<T> : IRepository<T, int> where T : IEntity<int>
{
}
