using Wms.Core.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Wms.Core.Infrastructure.Extensions;

/// <summary>
/// IQueryable 分页扩展
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// 将查询转换为分页结果
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="query">查询源</param>
    /// <param name="pageNumber">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>分页结果</returns>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize)
    {
        var totalCount = await query.CountAsync();

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;

        var data = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return PagedResult.Create(data, pageNumber, pageSize, totalCount);
    }

    /// <summary>
    /// 将查询转换为分页结果（同步版本）
    /// </summary>
    public static PagedResult<T> ToPagedResult<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize)
    {
        var totalCount = query.Count();

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;

        var data = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return PagedResult.Create(data, pageNumber, pageSize, totalCount);
    }
}
