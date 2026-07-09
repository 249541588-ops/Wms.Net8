using Hangfire;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Jobs;
using Wms.Core.WebApi.Security;
using Wms.Core.WebApi.Services;

namespace Wms.Core.WebApi.Extensions;

public static class HangfireExtensions
{
    /// <summary>
    /// 注册 Hangfire 任务调度（使用 SQL Server 存储）
    /// </summary>
    public static IServiceCollection AddWmsHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        // 注册 Hangfire 任务调度（使用 SQL Server 存储）
        var hangfireConnectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddHangfire(x => x
            .UseSqlServerStorage(hangfireConnectionString));
        services.AddHangfireServer();

        // 注册定时任务调度器
        services.AddSingleton<IJobDispatcher, JobDispatcher>();
        services.AddScoped<BackgroundJobService>();
        services.AddScoped<DataCleanupService>();
        // 注册命名 HttpClient，配置 BaseAddress（用于 http-call 模式应用自调用）
        // Q403 SSRF 防御：禁止自动重定向，防止 302 重定向到内网/任意主机
        services.AddHttpClient("job-http", client =>
        {
            var baseUrl = configuration["App:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler
        {
            // 禁止自动跟随重定向 - 防止 SSRF via 302
            AllowAutoRedirect = false,
            // 不使用系统默认代理 - 防止通过代理绕过 SSRF 防护
            UseProxy = false
        });

        return services;
    }

    /// <summary>
    /// 配置 Hangfire 仪表盘（仅允许白名单 IP 访问）并同步定时任务
    /// </summary>
    public static async Task<WebApplication> UseWmsHangfireDashboard(this WebApplication app, IConfiguration configuration)
    {
        // 配置 Hangfire 仪表盘（仅允许白名单 IP 访问）
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireIpAuthorizationFilter(configuration) }
        });

        // 清理旧版 Hangfire 定时任务（WcsTaskSyncJob 已删除）
        RecurringJob.RemoveIfExists("wcs-task-sync");

        // 启动时同步 DB 中的定时任务到 Hangfire
        using (var syncScope = app.Services.CreateScope())
        {
            var jobService = syncScope.ServiceProvider.GetRequiredService<BackgroundJobService>();
            await jobService.SyncAllAsync();

            // 自动种子 data-cleanup 任务（幂等：通过 ApiUrl 检查）
            var seedDb = syncScope.ServiceProvider.GetRequiredService<WmsDbContext>();
            var cleanupExists = await seedDb.BackgroundJobs
                .AnyAsync(j => j.ApiUrl == "data-cleanup" && j.IsDeleted != true);
            if (!cleanupExists)
            {
                await jobService.CreateAsync(
                    jobType: "internal",
                    name: "数据定时清理",
                    cron: "0 0 2 * * *",
                    description: "每天凌晨2点自动清理过期数据（归档表、操作日志、接口日志、WCS任务等）",
                    apiUrl: "data-cleanup");
            }
        }

        return app;
    }
}
