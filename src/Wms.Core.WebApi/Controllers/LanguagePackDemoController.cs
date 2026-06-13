using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Wms.Core.WebApi.Filters;
using Wms.Core.WebApi.Helpers;
using Wms.Core.WebApi.Middleware;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 语言包功能演示控制器
/// 展示如何在控制器中使用全局语言包
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class LanguagePackDemoController : ControllerBase
{
    private readonly ILogger<LanguagePackDemoController> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly ILanguagePackCacheTracker _cacheTracker;

    public LanguagePackDemoController(
        ILogger<LanguagePackDemoController> logger,
        IMemoryCache memoryCache,
        ILanguagePackCacheTracker cacheTracker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _cacheTracker = cacheTracker ?? throw new ArgumentNullException(nameof(cacheTracker));
    }

    /// <summary>
    /// 获取当前语言信息
    /// </summary>
    /// <returns>当前语言详情</returns>
    [HttpGet("CurrentLanguage")]
    [AllowAnonymous]
    public IActionResult GetCurrentLanguage()
    {
        var currentLang = HttpContext.GetCurrentLanguage();
        var version = HttpContext.GetLanguagePackVersion();

        return Ok(new
        {
            status = true,
            data = new
            {
                currentLanguage = currentLang,
                version = version,
                message = $"当前语言: {currentLang}, 版本: {version}"
            }
        });
    }

    /// <summary>
    /// 翻译单个文本
    /// </summary>
    /// <param name="text">需要翻译的中文文本</param>
    /// <returns>翻译后的文本</returns>
    [HttpGet("Translate")]
    [AllowAnonymous]
    public IActionResult Translate([FromQuery] string text)
    {
        var translated = HttpContext.Translate(text);
        var currentLang = HttpContext.GetCurrentLanguage();

        return Ok(new
        {
            status = true,
            data = new
            {
                original = text,
                translated = translated,
                language = currentLang
            }
        });
    }

    /// <summary>
    /// 批量翻译
    /// </summary>
    /// <param name="texts">需要翻译的中文文本数组</param>
    /// <returns>翻译后的文本数组</returns>
    [HttpPost("TranslateBatch")]
    [AllowAnonymous]
    public IActionResult TranslateBatch([FromBody] string[] texts)
    {
        var translations = HttpContext.TranslateBatch(texts);
        var currentLang = HttpContext.GetCurrentLanguage();

        return Ok(new
        {
            status = true,
            data = new
            {
                language = currentLang,
                translations = translations
            }
        });
    }

    /// <summary>
    /// 获取完整的语言包（从中间件注入）
    /// </summary>
    /// <returns>完整的语言包对象</returns>
    [HttpGet("FullPack")]
    [AllowAnonymous]
    public IActionResult GetFullPack()
    {
        var languagePack = HttpContext.GetLanguagePack();

        if (languagePack == null)
        {
            return NotFound(new
            {
                status = false,
                message = "语言包未找到，请确保 LanguagePackMiddleware 已正确注册"
            });
        }

        return Ok(new
        {
            status = true,
            data = languagePack
        });
    }

    /// <summary>
    /// 清除语言包缓存（管理员功能）
    /// </summary>
    /// <returns>操作结果</returns>
    [HttpPost("ClearCache")]
    [Authorize(Roles = "Admin")]
    public IActionResult ClearCache()
    {
        _memoryCache.ClearLanguagePackCache(_cacheTracker, _logger);

        return Ok(new
        {
            status = true,
            message = "语言包缓存已清除",
            clearedAt = DateTime.Now
        });
    }

    /// <summary>
    /// 获取缓存状态统计
    /// </summary>
    /// <returns>缓存统计信息</returns>
    [HttpGet("CacheStats")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetCacheStats()
    {
        var trackedKeys = _cacheTracker.GetTrackedKeys();
        var languagePackKeys = trackedKeys.ToList();

        return Ok(new
        {
            status = true,
            data = new
            {
                totalLanguagePackCaches = languagePackKeys.Count,
                cacheKeys = languagePackKeys,
                message = $"共有 {languagePackKeys.Count} 个语言包缓存项"
            }
        });
    }
}
