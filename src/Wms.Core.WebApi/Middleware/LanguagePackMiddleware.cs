using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Repositories;
using Wms.Core.WebApi.Helpers;

namespace Wms.Core.WebApi.Middleware;

/// <summary>
/// 语言包中间件 - 提供全局语言包访问功能
/// </summary>
public class LanguagePackMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LanguagePackMiddleware> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly ILanguagePackCacheTracker _cacheTracker;

    // 缓存配置
    private const string CACHE_KEY_PREFIX = "GlobalLanguagePack_";
    private const int CACHE_EXPIRATION_MINUTES = 30;

    public LanguagePackMiddleware(
        RequestDelegate next,
        ILogger<LanguagePackMiddleware> logger,
        IMemoryCache memoryCache,
        ILanguagePackCacheTracker cacheTracker)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _cacheTracker = cacheTracker ?? throw new ArgumentNullException(nameof(cacheTracker));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // 拦截语言包请求
        if (path.StartsWith("/api/v1/Sys_Language/LanguagePack", StringComparison.OrdinalIgnoreCase))
        {
            await HandleLanguagePackRequest(context);
            return;
        }

        // 将语言包注入到 HttpContext.Items 中，供后续使用
        await InjectLanguagePackToContext(context);

        await _next(context);
    }

    /// <summary>
    /// 处理语言包请求
    /// </summary>
    private async Task HandleLanguagePackRequest(HttpContext context)
    {
        try
        {
            // 从查询参数或 header 获取语言和模块
            var lang = context.Request.Query["lang"].ToString()?.ToLower() ?? "zh";
            var module = context.Request.Query["module"].ToString();
            var acceptLanguage = context.Request.Headers["Accept-Language"].FirstOrDefault()?.ToLower() ?? lang;

            // 优先使用 header 中的语言
            var currentLang = acceptLanguage;

            var cacheKey = $"{CACHE_KEY_PREFIX}{currentLang}_{module ?? "all"}";

            _logger.LogInformation("语言包请求 - 语言: {Language}, 模块: {Module}, CacheKey: {CacheKey}",
                currentLang, module ?? "全部", cacheKey);

            // 尝试从缓存获取
            if (_memoryCache.TryGetValue(cacheKey, out object? cachedResult))
            {
                _logger.LogInformation("从缓存返回语言包 - CacheKey: {CacheKey}", cacheKey);
                await WriteJsonResponse(context, cachedResult);
                return;
            }

            // 从数据库查询 - 使用 IServiceScope 创建 scope
            using (var scope = context.RequestServices.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IRepository<Sys_Language, int>>();

                _logger.LogInformation("缓存未命中，从数据库查询 - CacheKey: {CacheKey}", cacheKey);

                var query = repository.GetAll();

                // 按模块筛选
                if (!string.IsNullOrEmpty(module))
                {
                    query = query.Where(m => m.Module == module);
                }

                var languages = query.ToList();

                var result = new
                {
                    status = true,
                    message = "获取语言包成功",
                    data = new
                    {
                        lang = currentLang,
                        version = DateTime.Now.ToString("yyyyMMddHHmmss"),
                        data = languages,
                    }
                };

                // 存入缓存
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                    .RegisterPostEvictionCallback((key, value, reason, state) =>
                    {
                        _logger.LogInformation("语言包缓存过期 - Key: {Key}, Reason: {Reason}", key, reason);
                    });

                _memoryCache.Set(cacheKey, result, cacheEntryOptions);
                _cacheTracker.AddKey(cacheKey); // 跟踪缓存键

                _logger.LogInformation("语言包已缓存 - CacheKey: {CacheKey}, Count: {Count}", cacheKey, languages.Count);

                await WriteJsonResponse(context, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取语言包失败: {Message}", ex.Message);
            await WriteErrorResponse(context, $"获取语言包失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 将语言包注入到 HttpContext 中（供其他控制器/中间件使用）
    /// </summary>
    private async Task InjectLanguagePackToContext(HttpContext context)
    {
        try
        {
            // 获取当前请求的语言偏好
            var acceptLanguage = context.Request.Headers["Accept-Language"].FirstOrDefault()?.ToLower() ?? "zh";

            // 生成缓存键
            var cacheKey = $"{CACHE_KEY_PREFIX}{acceptLanguage}_all";

            // 从缓存或数据库获取语言包
            object? languagePack = null;

            if (_memoryCache.TryGetValue(cacheKey, out object? cachedResult))
            {
                languagePack = cachedResult;
            }
            else
            {
                // 从数据库查询 - 使用 IServiceScope 创建 scope
                using (var scope = context.RequestServices.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IRepository<Sys_Language, int>>();

                    // 从数据库查询
                    var languages = repository.GetAll().ToList();
                    languagePack = new
                    {
                        lang = acceptLanguage,
                        version = DateTime.Now.ToString("yyyyMMddHHmmss"),
                        data = languages,
                    };

                    // 存入缓存
                    _memoryCache.Set(cacheKey, languagePack, new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES)));
                    _cacheTracker.AddKey(cacheKey); // 跟踪缓存键
                }
            }

            // 注入到 HttpContext.Items 中
            context.Items["LanguagePack"] = languagePack;
            context.Items["CurrentLanguage"] = acceptLanguage;

            _logger.LogDebug("语言包已注入到 HttpContext - Language: {Language}", acceptLanguage);
        }
        catch (Exception ex)
        {
            // 注入失败不影响主流程
            _logger.LogWarning(ex, "注入语言包到 HttpContext 失败: {Message}", ex.Message);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 写入 JSON 响应
    /// </summary>
    private async Task WriteJsonResponse(HttpContext context, object data)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status200OK;

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }

    /// <summary>
    /// 写入错误响应
    /// </summary>
    private async Task WriteErrorResponse(HttpContext context, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var errorResponse = new
        {
            status = false,
            message = message,
            path = context.Request.Path.Value
        };

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// 语言包中间件扩展方法
/// </summary>
public static class LanguagePackMiddlewareExtensions
{
    /// <summary>
    /// 使用语言包中间件
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    /// <returns>应用程序构建器</returns>
    public static IApplicationBuilder UseLanguagePack(this IApplicationBuilder app)
    {
        return app.UseMiddleware<LanguagePackMiddleware>();
    }

    /// <summary>
    /// 清除所有语言包缓存
    /// </summary>
    public static void ClearLanguagePackCache(this IMemoryCache memoryCache, ILanguagePackCacheTracker cacheTracker, ILogger? logger = null)
    {
        try
        {
            var keysToRemove = cacheTracker.GetTrackedKeys();

            foreach (var key in keysToRemove)
            {
                memoryCache.Remove(key);
                logger?.LogInformation("已清除语言包缓存: {CacheKey}", key);
            }

            // 清空跟踪列表
            cacheTracker.ClearKeys();

            logger?.LogInformation("语言包缓存已清除，共清除 {Count} 个缓存项", keysToRemove.Count);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "清除语言包缓存失败: {Message}", ex.Message);
        }
    }
}
