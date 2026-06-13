using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;
using System.Net.Http;

namespace Wms.Core.WebApi.HealthChecks;

/// <summary>
/// WCS（仓库控制系统）健康检查
/// </summary>
public class WcsHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WcsHealthCheck> _logger;
    private readonly string? _wcsEndpoint;

    public WcsHealthCheck(
        HttpClient httpClient,
        ILogger<WcsHealthCheck> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wcsEndpoint = configuration["Wcs:Endpoint"];
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 如果未配置 WCS 端点，返回健康状态
            if (string.IsNullOrEmpty(_wcsEndpoint))
            {
                data["status"] = "not_configured";
                data["message"] = "WCS endpoint is not configured";
                return HealthCheckResult.Healthy("WCS is not configured", data: data);
            }

            data["endpoint"] = _wcsEndpoint;

            // 发送健康检查请求
            var healthUrl = $"{_wcsEndpoint.TrimEnd('/')}/health";
            var request = new HttpRequestMessage(HttpMethod.Get, healthUrl);
            request.Headers.Add("Accept", "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            stopwatch.Stop();
            data["response_time_ms"] = stopwatch.ElapsedMilliseconds;
            data["status_code"] = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                data["status"] = "connected";
                return HealthCheckResult.Healthy("WCS is reachable", data: data);
            }

            if ((int)response.StatusCode >= 500)
            {
                data["error"] = $"WCS returned error: {response.StatusCode}";
                return HealthCheckResult.Unhealthy("WCS is experiencing errors", data: data);
            }

            data["error"] = $"WCS returned: {response.StatusCode}";
            return HealthCheckResult.Degraded("WCS is degraded", data: data);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "WCS health check failed - connection error");
            stopwatch.Stop();
            data["error"] = ex.Message;
            data["response_time_ms"] = stopwatch.ElapsedMilliseconds;
            return HealthCheckResult.Unhealthy("WCS is unreachable", exception: ex, data: data);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "WCS health check failed - timeout");
            stopwatch.Stop();
            data["error"] = "Request timeout";
            data["response_time_ms"] = stopwatch.ElapsedMilliseconds;
            return HealthCheckResult.Unhealthy("WCS request timeout", exception: ex, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WCS health check failed");
            stopwatch.Stop();
            data["error"] = ex.Message;
            data["response_time_ms"] = stopwatch.ElapsedMilliseconds;
            return HealthCheckResult.Unhealthy("WCS health check failed", exception: ex, data: data);
        }
    }
}
