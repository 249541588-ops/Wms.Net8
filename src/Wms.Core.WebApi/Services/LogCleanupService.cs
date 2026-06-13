using Microsoft.EntityFrameworkCore;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 日志自动清理服务：每天凌晨 2 点自动清理过期日志
/// SystemLogs 保留 30 天，InterfaceLogs 保留 60 天
/// </summary>
public class LogCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogCleanupService> _logger;

    public LogCleanupService(IServiceScopeFactory scopeFactory, ILogger<LogCleanupService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1).AddHours(2); // 每天凌晨 2 点
                var delay = nextRun - now;
                if (delay.TotalMilliseconds > 0)
                    await Task.Delay(delay, stoppingToken);

                _logger.LogInformation("[LogCleanup] 开始执行日志清理...");

                using var scope = _scopeFactory.CreateScope();

                // SystemLogs 保留 30 天
                var mainDb = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
                var sysDeleted = mainDb.Database.ExecuteSqlInterpolated($"DELETE FROM SystemLogs WHERE OperationTime < {DateTime.UtcNow.AddDays(-30)}");
                _logger.LogInformation("[LogCleanup] SystemLogs 清理完成，删除 {Count} 条", sysDeleted);

                // InterfaceLogs 保留 60 天
                var logDb = scope.ServiceProvider.GetRequiredService<WmsLogDbContext>();
                var intDeleted = logDb.Database.ExecuteSqlInterpolated($"DELETE FROM InterfaceLogs WHERE CreatedTime < {DateTime.UtcNow.AddDays(-60)}");
                _logger.LogInformation("[LogCleanup] InterfaceLogs 清理完成，删除 {Count} 条", intDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LogCleanup] 日志清理失败: {Message}", ex.Message);
                // 出错后等待 1 小时再重试，避免频繁报错
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
