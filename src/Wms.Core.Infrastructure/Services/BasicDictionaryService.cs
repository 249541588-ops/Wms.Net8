using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Extensions;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Requests;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.ValueObjects;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 
/// </summary>
public class BasicDictionaryService : IBasicDictionaryService
{

    private readonly WmsDbContext _db;
    private readonly ILogger<BasicDictionaryService> _logger;
    private readonly IRepository<BasicDictionary, int> _repository;
    private readonly ITranslationService _translationService;
    private readonly IMemoryCache _cache;

    /// <summary>
    ///
    /// </summary>
    /// <param name="db"></param>
    /// <param name="repository"></param>
    /// <param name="translationService"></param>
    /// <param name="logger"></param>
    /// <param name="cache"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public BasicDictionaryService(
        WmsDbContext db,
        IRepository<BasicDictionary, int> repository,
        ITranslationService translationService,
        ILogger<BasicDictionaryService> logger,
        IMemoryCache cache
        )
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="No"></param>
    /// <returns></returns>
    public List<BasicDictionary> GetItemsByNo(string No)
    {
        var cacheKey = $"DictItems_{No}";

        if (_cache.TryGetValue(cacheKey, out List<BasicDictionary>? cachedItems))
        {
            return cachedItems!;
        }

        var parent = _db.Set<BasicDictionary>()
            
            .Where(x => x.No == No && x.Status == 1)
            .SingleOrDefault();

        if (parent == null)
            return new List<BasicDictionary>();

        var items = _db.Set<BasicDictionary>()
            
            .Where(x => x.ParentId == parent.Id && x.Status == 1)
            .OrderBy(x => x.Sort)
            .ToList();

        _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(CacheKeyTypes.CacheKeyTime)
        });

        return items;
    }

    /// <summary>
    /// 清除指定编码的缓存
    /// </summary>
    /// <param name="no"></param>
    public void ClearCache(string no)
    {
        _cache.Remove($"Dict_{no}");
        _cache.Remove($"DictItems_{no}");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="No"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public BasicDictionary GetByNo(string No)
    {
        var cacheKey = $"Dict_{No}";

        if (_cache.TryGetValue(cacheKey, out BasicDictionary? cachedItem))
        {
            return cachedItem!;
        }

        var item = _db.Set<BasicDictionary>()
            
            .FirstOrDefault(x => x.No == No &&  x.Status == 1);

        if (item != null)
        {
            _cache.Set(cacheKey, item, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(CacheKeyTypes.CacheKeyTime)
            });
        }

        return item!;
    }
}
