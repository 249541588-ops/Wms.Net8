using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Entities.Warehouse;
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
public class LocationService : ILocationService
{
    
    private readonly WmsDbContext _db;
    private readonly ILogger<LocationService> _logger;
    private readonly IRepository<Location, int> _repository;
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
    public LocationService(
        WmsDbContext db,
        IRepository<Location, int> repository,
        ITranslationService translationService,
        ILogger<LocationService> logger,
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
    /// <param name="locationCode"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Location GetLocation(string locationCode)
    {
        if (string.IsNullOrEmpty(locationCode))
            return null!;

        var cacheKey = $"Location_{locationCode}";

        if (_cache.TryGetValue(cacheKey, out Location? cachedLocation))
            return cachedLocation!;

        var location = _repository.FirstOrDefault(l => l.LocationCode == locationCode);

        if (location != null)
        {
            _cache.Set(cacheKey, location, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(CacheKeyTypes.CacheKeyTime)
            });
        }

        return location!;
    }
}
