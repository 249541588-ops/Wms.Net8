using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace Wms.Core.WebApi.HealthChecks;

/// <summary>
/// 磁盘空间健康检查
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly ILogger<DiskSpaceHealthCheck> _logger;
    private readonly long _minFreeSpaceBytes;

    public DiskSpaceHealthCheck(
        ILogger<DiskSpaceHealthCheck> logger,
        long minFreeSpaceBytes = 5L * 1024 * 1024 * 1024) // 默认 5GB
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _minFreeSpaceBytes = minFreeSpaceBytes;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory)!);

            var totalBytes = drive.TotalSize;
            var freeBytes = drive.AvailableFreeSpace;
            var usedBytes = totalBytes - freeBytes;
            var freePercent = (double)freeBytes / totalBytes * 100;

            data["drive"] = drive.Name;
            data["total_gb"] = Math.Round(totalBytes / 1024.0 / 1024 / 1024, 2);
            data["free_gb"] = Math.Round(freeBytes / 1024.0 / 1024 / 1024, 2);
            data["used_gb"] = Math.Round(usedBytes / 1024.0 / 1024 / 1024, 2);
            data["free_percent"] = Math.Round(freePercent, 2);
            data["min_free_gb"] = Math.Round(_minFreeSpaceBytes / 1024.0 / 1024 / 1024, 2);
            data["drive_format"] = drive.DriveFormat;
            data["drive_type"] = drive.DriveType.ToString();

            stopwatch.Stop();
            data["response_time_ms"] = stopwatch.ElapsedMilliseconds;

            // 判断健康状态
            if (freeBytes < _minFreeSpaceBytes)
            {
                var message = $"Low disk space: {Math.Round(freeBytes / 1024.0 / 1024 / 1024, 2)} GB free (minimum: {Math.Round(_minFreeSpaceBytes / 1024.0 / 1024 / 1024, 2)} GB)";
                _logger.LogWarning(message);

                if (freePercent < 5) // 少于 5%
                {
                    return HealthCheckResult.Unhealthy("Critical: Disk space is very low", data: data);
                }

                return HealthCheckResult.Degraded("Warning: Low disk space", data: data);
            }

            if (freePercent < 10) // 少于 10%
            {
                return HealthCheckResult.Degraded("Warning: Disk space is below 10%", data: data);
            }

            return HealthCheckResult.Healthy("Disk space is sufficient", data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disk space health check failed");
            stopwatch.Stop();
            data["error"] = ex.Message;
            data["response_time_ms"] = stopwatch.ElapsedMilliseconds;
            return HealthCheckResult.Unhealthy("Disk space health check failed", exception: ex, data: data);
        }
    }
}
