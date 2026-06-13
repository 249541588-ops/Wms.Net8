using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Infrastructure.Persistence;
using System.Diagnostics;

namespace Wms.Core.WebApi.HealthChecks;

/// <summary>
/// WMS 系统健康检查
/// </summary>
public class WmsHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WmsHealthCheck> _logger;

    /// <summary>
    /// 初始化 WMS 健康检查类的新实例
    /// </summary>
    public WmsHealthCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<WmsHealthCheck> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 检查数据库连接（在 Scope 内解析 WmsDbContext）
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
                var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                if (!canConnect)
                {
                    data["database"] = "unhealthy";
                    return HealthCheckResult.Unhealthy("Database connection failed", data: data);
                }

                data["database"] = "healthy";
                data["database_response_time_ms"] = stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                data["database"] = "unhealthy";
                data["database_error"] = ex.Message;
                return HealthCheckResult.Unhealthy("Database connection failed", data: data);
            }

            // 检查内存使用情况
            var currentProcess = Process.GetCurrentProcess();
            data["memory_mb"] = currentProcess.WorkingSet64 / 1024 / 1024;
            data["cpu_time_ms"] = currentProcess.TotalProcessorTime.TotalMilliseconds;

            stopwatch.Stop();
            data["total_response_time_ms"] = stopwatch.ElapsedMilliseconds;

            return HealthCheckResult.Healthy("System is operating normally", data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");
            stopwatch.Stop();
            data["error"] = ex.Message;
            data["response_time_ms"] = stopwatch.ElapsedMilliseconds;
            return HealthCheckResult.Unhealthy("Health check failed", exception: ex, data: data);
        }
    }
}
