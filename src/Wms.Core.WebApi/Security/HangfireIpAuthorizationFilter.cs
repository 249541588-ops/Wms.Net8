using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;

namespace Wms.Core.WebApi.Security;

/// <summary>
/// Hangfire Dashboard IP 白名单鉴权过滤器
/// 仅允许配置的 IP 地址访问 Dashboard
/// </summary>
public class HangfireIpAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly string[] _allowedIps;

    public HangfireIpAuthorizationFilter(IConfiguration config)
    {
        _allowedIps = config.GetSection("Hangfire:AllowedIps").Get<string[]>()
            ?? new[] { "127.0.0.1", "::1" };
    }

    public bool Authorize(DashboardContext context)
    {
        // null IP 时拒绝（防止代理配置错误导致全部放行）
        if (context.Request.LocalIpAddress == null)
            return false;

        return _allowedIps.Contains(context.Request.LocalIpAddress.ToString());
    }
}
