using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Security;
using Wms.Core.WebApi.Services.Wcs;
using Wms.Core.WebApi.Services;
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
                "*/30 * * * * *"),
            ["data-cleanup"] = new InternalMethodInfo(
                "data-cleanup",
                "数据定时清理",
                "清理归档数据、操作日志、接口日志、WCS历史任务等（保留天数可在 appsettings.json 配置）",
                "0 0 2 * * *")
        };

        _internalHandlers = new Dictionary<string, Func<IServiceProvider, Task>>(StringComparer.OrdinalIgnoreCase)
        {
            ["wcs-task-sync"] = async sp =>
            {
                var syncService = sp.GetRequiredService<WcsTaskSyncService>();
                await syncService.SyncStatusAsync();
                await syncService.RetryUnsentTasksAsync();
            },
            ["data-cleanup"] = async sp =>
            {
                var cleanupService = sp.GetRequiredService<DataCleanupService>();
                await cleanupService.CleanupAllAsync();
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
    /// HTTP API 调用（带安全限制）。
    /// Q403 三层防御：
    /// 1. URL 白名单（HttpCallSafety.IsValidHttpCallUrl）- 即使数据库被篡改也会拒绝
    /// 2. Headers 黑名单（HttpCallSafety.IsForbiddenHeader）- 防止 Authorization/Cookie/Host/X-Forwarded-* 注入
    /// 3. HttpClient 禁止重定向（在 HangfireExtensions 配置）- 防止 302 重定向到内网地址
    /// </summary>
    private async Task ExecuteHttpCallAsync(IServiceProvider sp, SysBackgroundJob job)
    {
        var url = job.ApiUrl ?? "";

        // 防御纵深：再次执行白名单校验（即使数据库被绕过/篡改）
        if (!HttpCallSafety.IsValidHttpCallUrl(url, out var urlReason))
        {
            _logger.LogWarning("[JobDispatcher] HTTP 任务 ApiUrl 校验失败: {Reason}, JobId={Id}, Url={Url}",
                urlReason, job.Id, url);
            return;
        }

        var method = job.RequestType ?? "GET";
        var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("job-http");
        if (httpClient.BaseAddress == null)
        {
            _logger.LogError("[JobDispatcher] 未配置 App:BaseUrl，HTTP 任务无法执行自调用, JobId={Id}, Url={Url}", job.Id, url);
            return;
        }
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        // 设置请求体
        if (!string.IsNullOrWhiteSpace(job.Payload) && method != "GET")
        {
            request.Content = new StringContent(job.Payload, Encoding.UTF8, "application/json");
        }

        // 设置额外请求头（防御纵深：黑名单过滤，即使数据库被篡改也会剔除敏感头）
        if (!string.IsNullOrWhiteSpace(job.JobArgs))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(job.JobArgs);
                if (headers != null)
                {
                    foreach (var (key, value) in headers)
                    {
                        if (HttpCallSafety.IsForbiddenHeader(key))
                        {
                            _logger.LogWarning("[JobDispatcher] 拒绝注入敏感请求头: {Header}, JobId={Id}", key, job.Id);
                            continue;
                        }
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
