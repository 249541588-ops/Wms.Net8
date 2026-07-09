using Wms.Core.Application.Ports;
using Wms.Core.WebApi.Helpers;
using Microsoft.AspNetCore.Http;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 翻译服务实现 - 通过 IHttpContextAccessor 获取当前请求上下文进行翻译
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TranslationService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Translate(string key, string? defaultValue = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return defaultValue ?? key;

        return httpContext.Translate(key, defaultValue);
    }
}
