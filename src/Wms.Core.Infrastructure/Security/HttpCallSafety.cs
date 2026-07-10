using System.Collections.Generic;
using System.Linq;

namespace Wms.Core.Infrastructure.Security;

/// <summary>
/// HTTP 调用安全工具 - SSRF 防御（路径白名单）+ Header 注入防御（黑名单）
/// </summary>
/// <remarks>
/// 用于 JobSchedule 的 http-call 模式：
/// 1. 限制 ApiUrl 仅能调用应用自身白名单内的相对路径端点（防 SSRF）
/// 2. 限制 Headers 不能注入 Authorization/Cookie/Host/X-Forwarded-* 等敏感/转发头
/// 配合 HttpClient 禁止重定向，构成完整 SSRF 防护。
/// </remarks>
public static class HttpCallSafety
{
    /// <summary>
    /// 允许的路径前缀（仅允许调用本应用 API）。
    /// </summary>
    public const string RequiredPathPrefix = "/api/";

    /// <summary>
    /// 禁止注入的 Header 名（大小写不敏感）。
    /// </summary>
    /// <remarks>
    /// - Authorization/Cookie/Proxy-Authorization：凭据相关，防止冒充其他用户
    /// - Host：影响路由
    /// - X-Forwarded-*/Forwarded/X-Real-IP：影响服务器日志与下游 IP 白名单校验
    /// - Origin/Referer：可能让 CSRF 校验失效
    /// - Set-Cookie：响应头，注入无意义且可能造成混乱
    /// </remarks>
    private static readonly HashSet<string> ForbiddenHeaders = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "Host",
        "Origin",
        "Referer",
        "X-Forwarded-For",
        "X-Forwarded-Host",
        "X-Forwarded-Proto",
        "X-Forwarded-Server",
        "X-Real-IP",
        "Forwarded",
        "Proxy-Authorization"
    };

    /// <summary>
    /// 校验 ApiUrl 是否符合 SSRF 防御要求。
    /// 仅允许 /api/ 开头的相对路径，禁止绝对 URL、协议相对 URL、反斜杠转义、路径遍历。
    /// </summary>
    /// <param name="url">待校验的 URL（用户输入）</param>
    /// <param name="reason">校验失败的原因（仅在校验失败时填充）</param>
    /// <returns>合法返回 true；否则 false</returns>
    public static bool IsValidHttpCallUrl(string? url, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(url))
        {
            reason = "API 地址不能为空";
            return false;
        }

        // 必须以 / 开头
        if (!url.StartsWith('/'))
        {
            reason = "API 地址必须是相对路径（以 / 开头）";
            return false;
        }

        // 禁止协议相对 URL（//evil.com）和反斜杠转义（/\evil.com）
        if (url.StartsWith("//") || url.StartsWith("/\\"))
        {
            reason = "禁止协议相对或反斜杠 URL";
            return false;
        }

        // 禁止任何形式的绝对 URL
        if (url.Contains("://", System.StringComparison.Ordinal))
        {
            reason = "禁止绝对 URL";
            return false;
        }

        // 禁止路径遍历（. 或 .. 段）
        var segments = url.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s == ".." || s == "."))
        {
            reason = "禁止路径遍历（. 或 ..）";
            return false;
        }

        // 必须以 /api/ 开头（限制只能调用应用自身 API）
        if (!url.StartsWith(RequiredPathPrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            reason = $"API 地址必须以 {RequiredPathPrefix} 开头（仅允许调用应用自身 API）";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 校验 Header 名是否在黑名单中（应禁止注入）。
    /// 支持 X-Forwarded-* 通配匹配。
    /// </summary>
    /// <param name="headerName">待校验的 Header 名</param>
    /// <returns>在黑名单中（应禁止）返回 true；否则 false</returns>
    public static bool IsForbiddenHeader(string? headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName)) return true;
        var name = headerName.Trim();

        // X-Forwarded-* 通配
        if (name.StartsWith("X-Forwarded-", System.StringComparison.OrdinalIgnoreCase))
            return true;

        return ForbiddenHeaders.Contains(name);
    }

    /// <summary>
    /// 从 Headers 字典中过滤出允许注入的键值对（移除黑名单项）。
    /// 用于防御纵深：即使数据库被绕过/篡改，执行时也会剔除危险 Header。
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> FilterSafeHeaders(
        IEnumerable<KeyValuePair<string, string>>? headers)
    {
        if (headers == null) return System.Array.Empty<KeyValuePair<string, string>>();
        return headers.Where(kvp => !IsForbiddenHeader(kvp.Key));
    }
}
