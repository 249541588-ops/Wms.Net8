using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace Wms.Core.WebApi.Helpers;

/// <summary>
/// 语言包辅助类 - 提供全局语言包访问功能
/// </summary>
public static class LanguagePackHelper
{
    private const string LANGUAGE_PACK_KEY = "LanguagePack";
    private const string CURRENT_LANGUAGE_KEY = "CurrentLanguage";

    /// <summary>
    /// 从 HttpContext 中获取语言包
    /// </summary>
    /// <param name="httpContext">HTTP 上下文</param>
    /// <returns>语言包对象，如果不存在则返回 null</returns>
    public static object? GetLanguagePack(this HttpContext httpContext)
    {
        return httpContext?.Items.TryGetValue(LANGUAGE_PACK_KEY, out object? languagePack) == true
            ? languagePack
            : null;
    }

    /// <summary>
    /// 获取当前请求的语言
    /// </summary>
    /// <param name="httpContext">HTTP 上下文</param>
    /// <returns>当前语言代码（如 zh, en, de）</returns>
    public static string GetCurrentLanguage(this HttpContext httpContext)
    {
        var language = httpContext?.Items.TryGetValue(CURRENT_LANGUAGE_KEY, out object? langObj) == true
            ? langObj?.ToString()
            : httpContext?.Request.Headers["Accept-Language"].FirstOrDefault();

        return language?.ToLower() ?? "zh";
    }

    /// <summary>
    /// 根据键获取翻译值
    /// </summary>
    /// <param name="httpContext">HTTP 上下文</param>
    /// <param name="key">翻译键（中文原文）</param>
    /// <param name="defaultValue">默认值（如果找不到翻译）</param>
    /// <returns>翻译后的值</returns>
    public static string? Translate(this HttpContext httpContext, string key, string? defaultValue = null)
    {
        var languagePack = httpContext.GetLanguagePack();
        if (languagePack == null) return defaultValue ?? key;

        // 使用反射访问 data 属性
        var dataProperty = languagePack.GetType().GetProperty("data");
        if (dataProperty == null) return defaultValue ?? key;

        var data = dataProperty.GetValue(languagePack);
        if (data == null) return defaultValue ?? key;
               
        var languages = data as System.Collections.IEnumerable; 
        if (languages == null) return defaultValue ?? key;

        var currentLang = httpContext.GetCurrentLanguage();

        // 遍历语言列表查找匹配的翻译
        foreach (var item in languages)
        {
            if (item == null) continue;

            var itemProps = item.GetType().GetProperties();
            string? chinese = null;
            string? english = null;
            string? deutsch = null;
            string? indonesian = null;

            foreach (var prop in itemProps)
            {
                var value = prop.GetValue(item)?.ToString();
                if (value == null) continue;

                switch (prop.Name.ToUpper())
                {
                    case "CHINESE":
                        chinese = value;
                        break;
                    case "ENGLISH":
                        english = value;
                        break;
                    case "DEUTSCH":
                        deutsch = value;
                        break;
                    case "INDONESIAN":
                        indonesian = value;
                        break;
                }
            }

            // 找到匹配的中文键
            if (chinese == key)
            {
                return currentLang switch
                {
                    "en" => english ?? chinese,
                    "de" => deutsch ?? chinese,
                    "id" => indonesian ?? chinese,
                    _ => chinese
                };
            }
        }

        return defaultValue ?? key;
    }

    /// <summary>
    /// 批量翻译
    /// </summary>
    /// <param name="httpContext">HTTP 上下文</param>
    /// <param name="keys">需要翻译的键列表</param>
    /// <returns>翻译后的键值对字典</returns>
    public static Dictionary<string, string?> TranslateBatch(this HttpContext httpContext, IEnumerable<string> keys)
    {
        var result = new Dictionary<string, string?>();

        foreach (var key in keys)
        {
            result[key] = httpContext.Translate(key);
        }

        return result;
    }

    /// <summary>
    /// 获取语言包版本号
    /// </summary>
    /// <param name="httpContext">HTTP 上下文</param>
    /// <returns>版本号字符串</returns>
    public static string GetLanguagePackVersion(this HttpContext httpContext)
    {
        var languagePack = httpContext.GetLanguagePack();
        if (languagePack == null) return "unknown";

        var dataProperty = languagePack.GetType().GetProperty("data");
        if (dataProperty == null) return "unknown";

        var data = dataProperty.GetValue(languagePack);
        if (data == null) return "unknown";

        var versionProperty = data.GetType().GetProperty("version");
        return versionProperty?.GetValue(data)?.ToString() ?? "unknown";
    }
}
