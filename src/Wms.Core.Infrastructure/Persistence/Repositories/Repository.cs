using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Interfaces;
using Wms.Core.Domain.Repositories;
using Wms.Core.Infrastructure.Security;

namespace Wms.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// 通用仓储实现
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class Repository<T, TKey> : IRepository<T, TKey>, IBulkRepository<T, TKey> where T : class, IEntity<TKey>
{
    protected readonly WmsDbContext _db;

    /// <summary>
    /// 初始化仓储类的新实例
    /// </summary>
    /// <param name="db">EF Core 数据库上下文</param>
    public Repository(WmsDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 根据ID获取实体
    /// </summary>
    public virtual T? GetById(TKey id)
    {
        return _db.Set<T>().Find(id);
    }

    /// <summary>
    /// 根据ID列表获取实体
    /// </summary>
    public virtual IEnumerable<T> GetByIds(IEnumerable<TKey> ids)
    {
        return _db.Set<T>().Where(x => ids.Contains(x.Id)).ToList();
    }

    /// <summary>
    /// 获取所有实体
    /// </summary>
    public virtual IQueryable<T> GetAll()
    {
        return _db.Set<T>();
    }

    /// <summary>
    /// 根据条件查找实体
    /// </summary>
    public virtual IQueryable<T> Find(Expression<Func<T, bool>> predicate)
    {
        return _db.Set<T>().Where(predicate);
    }

    /// <summary>
    /// 根据条件查询单个实体
    /// </summary>
    public virtual T? FirstOrDefault(Expression<Func<T, bool>> predicate)
    {
        return _db.Set<T>().FirstOrDefault(predicate);
    }

    /// <summary>
    /// 添加实体
    /// </summary>
    public virtual T Add(T entity)
    {
        _db.Set<T>().Add(entity);
        _db.SaveChanges();
        return entity;
    }

    /// <summary>
    /// 批量添加实体
    /// </summary>
    public virtual IEnumerable<T> AddRange(IEnumerable<T> entities)
    {
        _db.Set<T>().AddRange(entities);
        _db.SaveChanges();
        return entities;
    }

    /// <summary>
    /// 更新实体
    /// </summary>
    public virtual void Update(T entity)
    {
        _db.Set<T>().Update(entity);
        _db.SaveChanges();
    }

    /// <summary>
    /// 删除实体
    /// </summary>
    public virtual void Delete(T entity)
    {
        _db.Set<T>().Remove(entity);
        _db.SaveChanges();
    }

    /// <summary>
    /// 根据ID删除实体
    /// </summary>
    public virtual void Delete(TKey id)
    {
        var entity = GetById(id);
        if (entity != null)
        {
            _db.Set<T>().Remove(entity);
            _db.SaveChanges();
        }
    }

    /// <summary>
    /// 批量删除实体
    /// </summary>
    public virtual void DeleteRange(IEnumerable<T> entities)
    {
        _db.Set<T>().RemoveRange(entities);
        _db.SaveChanges();
    }

    /// <summary>
    /// 统计实体数量
    /// </summary>
    public virtual int Count()
    {
        return _db.Set<T>().Count();
    }

    /// <summary>
    /// 根据条件统计实体数量
    /// </summary>
    public virtual int Count(Expression<Func<T, bool>> predicate)
    {
        return _db.Set<T>().Count(predicate);
    }

    /// <summary>
    /// 检查实体是否存在
    /// </summary>
    public virtual bool Exists(Expression<Func<T, bool>> predicate)
    {
        return _db.Set<T>().Any(predicate);
    }

    /// <summary>
    /// 使用原生 SQL 查询（带参数，防止 SQL 注入）
    /// </summary>
    /// <param name="sql">SQL 查询语句</param>
    /// <param name="parameters">查询参数</param>
    /// <returns>查询结果列表</returns>
    public virtual IEnumerable<T> ExecuteSql(string sql, Dictionary<string, object>? parameters = null)
    {
        var args = parameters?.Select(p => new Microsoft.Data.SqlClient.SqlParameter(p.Key, p.Value)).ToArray();
        return _db.Set<T>().FromSqlRaw(sql, args ?? Array.Empty<object>()).ToList();
    }

    /// <summary>
    /// 使用原生 SQL 查询并分页（带参数，防止 SQL 注入）
    /// </summary>
    public virtual (IEnumerable<T> Data, int TotalCount, int TotalPages) ExecuteSqlPaged(
        string sql,
        string countSql,
        Dictionary<string, object>? parameters,
        int pageNumber,
        int pageSize,
        string orderBy = "Id ASC")
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;

        var safeOrderBy = SanitizeOrderBy(orderBy);

        // 执行总数查询
        var countArgs = parameters?.Select(p => new Microsoft.Data.SqlClient.SqlParameter(p.Key, p.Value)).ToArray();
        var totalCount = Convert.ToInt32(_db.Database.SqlQueryRaw<int>(countSql, countArgs ?? Array.Empty<object>()).First());

        if (totalCount == 0)
        {
            return (Enumerable.Empty<T>(), 0, 0);
        }

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var offset = (pageNumber - 1) * pageSize;
        var pagedSql = $@"
            {sql}
            ORDER BY {safeOrderBy}
            OFFSET {offset} ROWS
            FETCH NEXT {pageSize} ROWS ONLY
        ";

        var dataArgs = parameters?.Select(p => new Microsoft.Data.SqlClient.SqlParameter(p.Key, p.Value)).ToArray();
        var data = _db.Set<T>().FromSqlRaw(pagedSql, dataArgs ?? Array.Empty<object>()).ToList();

        return (data, totalCount, totalPages);
    }

    /// <summary>
    /// 校验 orderBy 参数，防止 SQL 注入（仅允许字母/数字/下划线的列名 + ASC/DESC 方向）。
    /// 白名单规则统一复用 <see cref="Wms.Core.Infrastructure.Security.SqlSafety"/>。
    /// </summary>
    private static string SanitizeOrderBy(string orderBy, string fallback = "Id ASC")
    {
        if (string.IsNullOrWhiteSpace(orderBy)) return fallback;

        var clauses = orderBy.Split(',');
        var sanitized = new List<string>();

        foreach (var clause in clauses)
        {
            var parts = clause.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Length > 2) continue;

            var column = parts[0];
            var direction = parts.Length == 2 ? parts[1] : "ASC";

            if (!SqlSafety.IsValidOrderByColumn(column)) continue;
            if (!SqlSafety.IsValidSortDirection(direction)) continue;

            sanitized.Add($"{column} {direction}");
        }

        return sanitized.Count > 0 ? string.Join(", ", sanitized) : fallback;
    }

    /// <summary>
    /// 批量更新（直接在数据库执行，绕过 Change Tracker，不触发审计字段自动填充）
    /// </summary>
    public virtual async Task<int> BulkUpdateAsync(
        Expression<Func<T, bool>> predicate,
        Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setters)
    {
        return await _db.Set<T>()
            .Where(predicate)
            .ExecuteUpdateAsync(setters);
    }

    /// <summary>
    /// 批量删除（直接在数据库执行，绕过 Change Tracker，不触发审计字段自动填充）
    /// </summary>
    public virtual async Task<int> BulkDeleteAsync(Expression<Func<T, bool>> predicate)
    {
        return await _db.Set<T>()
            .Where(predicate)
            .ExecuteDeleteAsync();
    }
}

/// <summary>
/// 整数主键的仓储实现
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public class Repository<T> : Repository<T, int>, IRepository<T> where T : class, IEntity<int>
{
    /// <summary>
    /// 初始化仓储类的新实例
    /// </summary>
    /// <param name="db">EF Core 数据库上下文</param>
    public Repository(WmsDbContext db) : base(db)
    {
    }
}
