using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace Wms.Core.WebApi.Filters;

/// <summary>
/// 内部接口 IP 白名单过滤器
/// 仅允许配置的 IP 地址访问标注此属性的端点
/// 用于 WCS 设备接口等内部 API 的访问控制
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class InternalIpWhitelistAttribute : ActionFilterAttribute
{
    private static string[]? _allowedIps;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        _allowedIps ??= context.HttpContext
            .RequestServices
            .GetRequiredService<IConfiguration>()
            .GetSection("Wcs:AllowedIps")
            .Get<string[]>();

        // 无配置时放行（避免未配置导致所有内部接口不可用）
        if (_allowedIps == null || _allowedIps.Length == 0)
        {
            return;
        }

        var remoteIp = GetClientIpAddress(context.HttpContext);
        if (remoteIp == null || !_allowedIps.Contains(remoteIp))
        {
            context.Result = new ObjectResult(new
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Message = "Access denied: IP not in whitelist"
            });
        }
    }

    /// <summary>
    /// 获取客户端真实 IP（优先从 X-Forwarded-For 提取）
    /// </summary>
    private static string? GetClientIpAddress(HttpContext context)
    {
        // 优先从反向代理 header 获取
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For 可能包含多个 IP（client, proxy1, proxy2），取第一个
            var commaIndex = forwardedFor.IndexOf(',');
            return commaIndex >= 0
                ? forwardedFor[..commaIndex].Trim()
                : forwardedFor.Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
