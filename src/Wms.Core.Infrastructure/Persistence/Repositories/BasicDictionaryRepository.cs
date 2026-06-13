using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.ValueObjects;

namespace Wms.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// 基础数据仓储实现
/// </summary>
public class BasicDictionaryRepository : IBasicDictionaryRepository
{
    private readonly WmsDbContext _db;

    /// <summary>
    /// 初始化仓储类的新实例
    /// </summary>
    /// <param name="session">NHibernate会话</param>
    public BasicDictionaryRepository(WmsDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 获取数据字典数据
    /// </summary>
    /// <param name="lx">1.接口 2.系统</param>
    /// <param name="type">1.通过no获取 2.通过name获取</param>
    /// <param name="name">传入参数</param>
    /// <returns></returns>
    public BasicDictionary GetBasicDictionary(int lx, int type, string name)
    {
        return null;
        //var cacheKey = CacheKeyTypes.BasicValueCacheKey + lx + "-" + type + "-" + name;
        //var result = CacheFactory.Cache().GetCache<BasicDictionary>(cacheKey);
        //if (result != null)
        //{
        //    return result;
        //}
        //else
        //{
        //    string sql = "";
        //    if (type == 1)
        //    {
        //        sql = "select * from BasicDictionary where No = @name and Status = 1";
        //    }
        //    else
        //    {
        //        sql = "select * from BasicDictionary where Name = @name and Status = 1";
        //    }
        //    using (var conn = new SqlConnection(connectionString))
        //    {
        //        result = conn.QueryFirstOrDefault<BasicDictionary2>(sql, new { name });
        //        if (result != null)
        //        {
        //            //加入缓存.2小时内有效
        //            CacheFactory.Cache().WriteCache(result, cacheKey, DateTime.Now.AddHours(CacheKeyTypes.CacheKeyTime));
        //        }
        //    }
        //    return result;
        //}
    }

    /// <summary>
    /// 通过编码获取父级节点下的子节点
    /// </summary>
    /// <param name="lx"></param>
    /// <param name="no"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public List<BasicDictionary> GetChildListByNo(int lx, string no)
    {
        return _db.Set<BasicDictionary>()
            .Where(x => _db.Set<BasicDictionary>().Any(t2 => t2.No == no && t2.Id == x.ParentId))
            .ToList();
    }

    /// <summary>
    /// 通过编码获取
    /// </summary>
    /// <param name="lx">1.api 2.系统</param>
    /// <param name="no">编码</param>
    /// <returns></returns>
    public List<BasicDictionary> GetListByNo(int lx, string no)
    {
        // 直接从数据库查询
        var result = _db.Set<BasicDictionary>().Where(x => x.No == no).ToList();
        return result;
    }
}
