using Hangfire;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Security;
using Wms.Core.WebApi.Jobs;
using SysBackgroundJob = Wms.Core.Domain.Entities.System.BackgroundJob;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 后台任务管理服务（DB 驱动）
/// </summary>
public class BackgroundJobService
{
    private readonly WmsDbContext _db;
    private readonly IJobDispatcher _dispatcher;
    private readonly ILogger<BackgroundJobService> _logger;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="db"></param>
    /// <param name="dispatcher"></param>
    /// <param name="logger"></param>
    public BackgroundJobService(
        WmsDbContext db, 
        IJobDispatcher dispatcher, 
        ILogger<BackgroundJobService> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有任务列表
    /// </summary>
    public async Task<List<SysBackgroundJob>> GetAllAsync()
    {
        return await _db.BackgroundJobs
            .Where(j => j.IsDeleted != true)
            .OrderBy(j => j.DisplayOrder ?? 0)
            .ThenBy(j => j.CreatedTime)
            .ToListAsync();
    }

    /// <summary>
    /// 创建任务（DB + Hangfire）
    /// </summary>
    public async Task<SysBackgroundJob> CreateAsync(string jobType, string name, string cron, string? description = null,
        string? apiUrl = null, string? requestType = null, string? payload = null, string? jobArgs = null)
    {
        // Q403 SSRF 防御：http-call 模式校验 ApiUrl + Headers
        ValidateHttpCallSafety(jobType, apiUrl, jobArgs);

        var job = new SysBackgroundJob
        {
            Id = Guid.NewGuid(),
            JobType = jobType,
            Name = name,
            JobName = jobType,
            CronExpression = cron,
            State = 1,
            Description = description,
            ApiUrl = apiUrl,
            RequestType = requestType,
            Payload = payload,
            JobArgs = jobArgs,
            CreatedTime = DateTime.Now,
            IsDeleted = false
        };

        _db.BackgroundJobs.Add(job);
        await _db.SaveChangesAsync();

        RegisterHangfire(job);
        _logger.LogInformation("创建定时任务: {Id}, Type={Type}, Cron={Cron}", job.Id, jobType, cron);
        return job;
    }

    /// <summary>
    /// 修改 Cron 表达式
    /// </summary>
    public async Task UpdateCronAsync(Guid id, string cron, string? description = null)
    {
        var job = await _db.BackgroundJobs.FindAsync(id)
            ?? throw new KeyNotFoundException($"未找到任务: {id}");

        job.CronExpression = cron;
        if (description != null) job.Description = description;
        job.ModifiedTime = DateTime.Now;
        await _db.SaveChangesAsync();

        if (job.State == 1)
        {
            RegisterHangfire(job);
            _logger.LogInformation("修改任务频率: {Id}, Cron={Cron}", id, cron);
        }
    }

    /// <summary>
    /// 完整更新任务（所有可编辑字段）
    /// </summary>
    public async Task UpdateAsync(Guid id, string jobType, string name, string cron, string? description = null,
        string? apiUrl = null, string? requestType = null, string? payload = null, string? jobArgs = null)
    {
        var job = await _db.BackgroundJobs.FindAsync(id)
            ?? throw new KeyNotFoundException($"未找到任务: {id}");

        // Q403 SSRF 防御：http-call 模式校验 ApiUrl + Headers（同 CreateAsync）
        ValidateHttpCallSafety(jobType, apiUrl, jobArgs);

        job.JobType = jobType;
        job.JobName = jobType;
        job.Name = name;
        job.CronExpression = cron;
        job.Description = description;
        job.ApiUrl = apiUrl;
        job.RequestType = requestType;
        job.Payload = payload;
        job.JobArgs = jobArgs;
        job.ModifiedTime = DateTime.Now;

        await _db.SaveChangesAsync();

        // 运行中的任务需要重新注册 Hangfire（Cron 或模式可能变了）
        if (job.State == 1)
        {
            RegisterHangfire(job);
            _logger.LogInformation("更新任务: {Id}, Type={Type}, Cron={Cron}", id, jobType, cron);
        }
    }

    /// <summary>
    /// 暂停任务
    /// </summary>
    public async Task PauseAsync(Guid id)
    {
        var job = await _db.BackgroundJobs.FindAsync(id)
            ?? throw new KeyNotFoundException($"未找到任务: {id}");

        job.State = 0;
        job.ModifiedTime = DateTime.Now;
        await _db.SaveChangesAsync();

        RecurringJob.RemoveIfExists(id.ToString());
        _logger.LogInformation("暂停任务: {Id}", id);
    }

    /// <summary>
    /// 恢复任务
    /// </summary>
    public async Task ResumeAsync(Guid id, string? cron = null)
    {
        var job = await _db.BackgroundJobs.FindAsync(id)
            ?? throw new KeyNotFoundException($"未找到任务: {id}");

        job.State = 1;
        job.ModifiedTime = DateTime.Now;
        if (!string.IsNullOrEmpty(cron))
            job.CronExpression = cron;
        await _db.SaveChangesAsync();

        RegisterHangfire(job);
        _logger.LogInformation("恢复任务: {Id}, Cron={Cron}", id, job.CronExpression);
    }

    /// <summary>
    /// 手动触发
    /// </summary>
    public async Task TriggerAsync(Guid id)
    {
        var job = await _db.BackgroundJobs.FindAsync(id)
            ?? throw new KeyNotFoundException($"未找到任务: {id}");

        RecurringJob.Trigger(id.ToString());
        _logger.LogInformation("触发任务: {Id}", id);
    }

    /// <summary>
    /// 删除任务
    /// </summary>
    public async Task DeleteAsync(Guid id)
    {
        var job = await _db.BackgroundJobs.FindAsync(id)
            ?? throw new KeyNotFoundException($"未找到任务: {id}");

        job.IsDeleted = true;
        job.ModifiedTime = DateTime.Now;
        await _db.SaveChangesAsync();

        RecurringJob.RemoveIfExists(id.ToString());
        _logger.LogInformation("删除任务: {Id}", id);
    }

    /// <summary>
    /// 启动时同步
    /// </summary>
    public async Task SyncAllAsync()
    {
        var activeJobs = await _db.BackgroundJobs
            .Where(j => j.State == 1 && j.IsDeleted != true && !string.IsNullOrEmpty(j.CronExpression))
            .ToListAsync();

        foreach (var job in activeJobs)
        {
            try
            {
                RegisterHangfire(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步任务到 Hangfire 失败: {Id}, Type={Type}", job.Id, job.JobType);
            }
        }

        _logger.LogInformation("启动同步完成，共注册 {Count} 个定时任务", activeJobs.Count);
    }

    /// <summary>
    /// 注册到 Hangfire（传 jobId）
    /// </summary>
    private void RegisterHangfire(SysBackgroundJob job)
    {
        RecurringJob.AddOrUpdate<JobDispatcher>(
            job.Id.ToString(),
            dispatcher => dispatcher.ExecuteAsync(job.Id),
            job.CronExpression ?? "0 */5 * * * *");
    }

    /// <summary>
    /// Q403 SSRF 防御校验：仅对 http-call 模式生效。
    /// 校验 ApiUrl（路径白名单）与 JobArgs/Headers（黑名单 Header）。
    /// 非法输入抛 <see cref="ArgumentException"/>，由 Controller 捕获并返回 400。
    /// </summary>
    private static void ValidateHttpCallSafety(string jobType, string? apiUrl, string? jobArgs)
    {
        if (jobType != "http-call") return;

        // 1. URL 白名单校验
        if (!HttpCallSafety.IsValidHttpCallUrl(apiUrl, out var urlReason))
        {
            throw new ArgumentException($"http-call 任务 ApiUrl 非法：{urlReason}");
        }

        // 2. Headers 黑名单校验（jobArgs 是 Headers JSON）
        if (!string.IsNullOrWhiteSpace(jobArgs))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(jobArgs);
                if (headers != null)
                {
                    var forbidden = headers.Keys.FirstOrDefault(HttpCallSafety.IsForbiddenHeader);
                    if (forbidden != null)
                    {
                        throw new ArgumentException(
                            $"http-call 任务 Headers 包含禁止的请求头 '{forbidden}'（不允许注入 Authorization/Cookie/Host/X-Forwarded-* 等敏感头）");
                    }
                }
            }
            catch (JsonException)
            {
                throw new ArgumentException("http-call 任务 Headers 必须是有效的 JSON 对象");
            }
        }
    }
}
