using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wms.Core.WebApi.Configuration;
using Wms.Core.WebApi.HealthChecks;

namespace Wms.Core.WebApi.Extensions;

public static class HealthCheckExtensions
{
    /// <summary>
    /// 配置健康检查选项和所有健康检查注册
    /// </summary>
    public static IServiceCollection AddWmsHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置健康检查选项
        services.Configure<HealthCheckOptions>(
            configuration.GetSection(HealthCheckOptions.SectionName));

        // 添加健康检查
        var redisSection = configuration.GetSection(RedisOptions.SectionName);
        var redisEnabled = redisSection.GetValue<bool>("Enabled");
        var redisConnectionString = redisSection["ConnectionString"];

        var healthChecksBuilder = services.AddHealthChecks()
            .AddSqlServer(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "sql-server",
                failureStatus: HealthStatus.Degraded)
            .AddCheck<WmsHealthCheck>("wms-system")
            .AddCheck<DiskSpaceHealthCheck>("disk-space", failureStatus: HealthStatus.Degraded)
            .AddCheck<WcsHealthCheck>("wcs", failureStatus: HealthStatus.Degraded)
            .AddCheck("self", () => HealthCheckResult.Healthy("API is running"));

        // 只有在启用 Redis 时才添加 Redis 健康检查
        if (redisEnabled && !string.IsNullOrEmpty(redisConnectionString))
        {
            healthChecksBuilder.AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Degraded);
        }

        // 添加健康检查 UI（简化版 - 不使用数据库）
        // 移除了 AddHealthChecksUI，改用手动配置的端点
        // 可以通过 /health 查看 JSON 格式的健康检查结果

        return services;
    }
}
