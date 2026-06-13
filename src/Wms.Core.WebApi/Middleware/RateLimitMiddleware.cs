using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Wms.Core.WebApi.Configuration;

namespace Wms.Core.WebApi.Middleware;

/// <summary>
/// 简单速率限制中间件
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly IMemoryCache _cache;
    private readonly RateLimitOptions _options;

    public RateLimitMiddleware(
        RequestDelegate next,
        ILogger<RateLimitMiddleware> logger,
        IMemoryCache cache,
        IOptions<RateLimitOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.EnableRateLimiting)
        {
            await _next(context);
            return;
        }

        // 获取客户端标识符
        var clientId = GetClientId(context);
        var path = context.Request.Path.Value ?? "/";

        // 检查端点特定规则
        var endpointRule = _options.EndpointRules
            .FirstOrDefault(r => path.Contains(r.Endpoint, StringComparison.OrdinalIgnoreCase) &&
                                 (r.Method == "*" || r.Method == context.Request.Method));

        var limit = endpointRule?.Limit ?? _options.PerClientLimit;
        var period = endpointRule?.Period ?? _options.PerClientSlidingWindow;

        // 生成缓存键
        var cacheKey = $"rate_limit_{clientId}_{path}_{context.Request.Method}";

        // 获取当前计数
        var counter = _cache.Get<int>(cacheKey);

        if (counter >= limit)
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId} on {Path}: {Counter}/{Limit}",
                clientId, path, counter, limit);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.Add("X-RateLimit-Limit", limit.ToString());
            context.Response.Headers.Add("X-RateLimit-Remaining", "0");
            context.Response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddSeconds(period).ToUnixTimeSeconds().ToString());

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = (int)HttpStatusCode.TooManyRequests,
                Title = "速率限制",
                Detail = $"请求过于频繁，请在 {period} 秒后重试。",
                Type = "https://tools.ietf.org/html/rfc6585#section-4"
            });

            return;
        }

        // 增加计数
        counter++;
        _cache.Set(cacheKey, counter, TimeSpan.FromSeconds(period));

        // 添加速率限制头
        context.Response.Headers.Add("X-RateLimit-Limit", limit.ToString());
        context.Response.Headers.Add("X-RateLimit-Remaining", (limit - counter).ToString());
        context.Response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddSeconds(period).ToUnixTimeSeconds().ToString());

        await _next(context);
    }

    private static string GetClientId(HttpContext context)
    {
        // 优先使用用户 ID（如果已认证）
        var userId = context.User?.FindFirst("sub")?.Value ??
                     context.User?.FindFirst("username")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        // 否则使用 IP 地址
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp != null)
        {
            return $"ip:{remoteIp}";
        }

        return "unknown";
    }
}
