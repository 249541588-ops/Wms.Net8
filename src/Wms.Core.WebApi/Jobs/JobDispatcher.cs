using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Services.Wcs;
using SysBackgroundJob = Wms.Core.Domain.Entities.System.BackgroundJob;

namespace Wms.Core.WebApi.Jobs;

/// <summary>
/// 定时任务调度器（混合模式：internal + http-call）
/// </summary>
public class JobDispatcher : IJobDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobDispatcher> _logger;
    private readonly Dictionary<string, InternalMethodInfo> _internalMethods;
    private readonly Dictionary<string, Func<IServiceProvider, Task>> _internalHandlers;

    public JobDispatcher(IServiceScopeFactory scopeFactory, ILogger<JobDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _internalMethods = new Dictionary<string, InternalMethodInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["wcs-task-sync"] = new InternalMethodInfo(
                "wcs-task-sync",
                "WCS 任务同步",
                "轮询 ctask.wcs_tasks 表同步状态 + 补发未下发任务",
                "*/30 * * * * *")
        };

        _internalHandlers = new Dictionary<string, Func<IServiceProvider, Task>>(StringComparer.OrdinalIgnoreCase)
        {
            ["wcs-task-sync"] = async sp =>
            {
                var syncService = sp.GetRequiredService<WcsTaskSyncService>();
                await syncService.SyncStatusAsync();
                await syncService.RetryUnsentTasksAsync();
            }
        };
    }

    public IReadOnlyList<InternalMethodInfo> GetInternalMethods() => _internalMethods.Values.ToList();

    public async Task ExecuteAsync(Guid jobId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var job = await db.BackgroundJobs.FindAsync(jobId);
        if (job == null)
        {
            _logger.LogError("[JobDispatcher] 未找到任务: {JobId}", jobId);
            return;
        }

        _logger.LogInformation("[JobDispatcher] 开始执行: {Name} (Id={Id}, Type={Type}) - {Time}",
            job.Name, jobId, job.JobType, DateTime.Now);

        try
        {
            switch ((job.JobType ?? "").ToLowerInvariant())
            {
                case "internal":
                    await ExecuteInternalAsync(scope.ServiceProvider, job);
                    break;
                case "http-call":
                    await ExecuteHttpCallAsync(scope.ServiceProvider, job);
                    break;
                default:
                    _logger.LogWarning("[JobDispatcher] 未知任务类型: {Type}, JobId={Id}", job.JobType, jobId);
                    break;
            }

            _logger.LogInformation("[JobDispatcher] 执行完成: {Name} - {Time}", job.Name, DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JobDispatcher] 执行失败: {Name} (Id={Id})", job.Name, jobId);
            throw;
        }
    }

    /// <summary>
    /// 内部方法直调
    /// </summary>
    private async Task ExecuteInternalAsync(IServiceProvider sp, SysBackgroundJob job)
    {
        var methodId = job.ApiUrl ?? "";
        if (!_internalHandlers.TryGetValue(methodId, out var handler))
        {
            _logger.LogError("[JobDispatcher] 未注册的内部方法: {Method}", methodId);
            return;
        }

        await handler(sp);
    }

    /// <summary>
    /// HTTP API 调用（带安全限制）
    /// </summary>
    private async Task ExecuteHttpCallAsync(IServiceProvider sp, SysBackgroundJob job)
    {
        var url = job.ApiUrl ?? "";
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("[JobDispatcher] HTTP 任务未配置 API 地址, JobId={Id}", job.Id);
            return;
        }

        // 安全校验：禁止绝对路径
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("//"))
        {
            _logger.LogWarning("[JobDispatcher] HTTP 任务使用绝对路径被拒绝: {Url}, JobId={Id}", url, job.Id);
            return;
        }

        var method = job.RequestType ?? "GET";
        var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        // 设置请求体
        if (!string.IsNullOrWhiteSpace(job.Payload) && method != "GET")
        {
            request.Content = new StringContent(job.Payload, Encoding.UTF8, "application/json");
        }

        // 设置额外请求头
        if (!string.IsNullOrWhiteSpace(job.JobArgs))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(job.JobArgs);
                if (headers != null)
                {
                    foreach (var (key, value) in headers)
                    {
                        request.Headers.TryAddWithoutValidation(key, value);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[JobDispatcher] 请求头 JSON 解析失败, JobId={Id}", job.Id);
            }
        }

        var response = await httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("[JobDispatcher] HTTP 响应: {StatusCode}, Body={Body}",
            (int)response.StatusCode, body.Length > 200 ? body[..200] + "..." : body);

        response.EnsureSuccessStatusCode();
    }
}
