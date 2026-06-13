using Hangfire;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Infrastructure.Persistence;
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
        // HTTP API 模式安全校验
        if (jobType == "http-call")
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new ArgumentException("HTTP API 模式必须指定 API 地址");
            if (apiUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                apiUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                apiUrl.StartsWith("//"))
                throw new ArgumentException("API 地址只允许相对路径（以 / 开头）");
        }

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
    public async Task UpdateCronAsync(Guid id, string cron)
    {
        var job = await _db.BackgroundJobs.FindAsync(id)
            ?? throw new KeyNotFoundException($"未找到任务: {id}");

        job.CronExpression = cron;
        job.ModifiedTime = DateTime.Now;
        await _db.SaveChangesAsync();

        if (job.State == 1)
        {
            RegisterHangfire(job);
            _logger.LogInformation("修改任务频率: {Id}, Cron={Cron}", id, cron);
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
            job.CronExpression ?? "*/30 * * * * *");
    }
}
