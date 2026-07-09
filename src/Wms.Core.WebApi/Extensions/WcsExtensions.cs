using Polly;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Tasks;
using Wms.Core.Infrastructure.Clients;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Tasks.Rules;
using Wms.Core.WebApi.HealthChecks;
using Wms.Core.WebApi.Services.Wcs;
using WcsReqHandler = Wms.Core.Application.Handlers.WcsRequest.IWcsRequestHandler;
using WcsReqHandlers = Wms.Core.Infrastructure.Handlers.WcsRequest;
using TskCompHandlers = Wms.Core.Infrastructure.Handlers.TaskCompletion;

namespace Wms.Core.WebApi.Extensions;

public static class WcsExtensions
{
    /// <summary>
    /// 注册 WCS 通信服务（任务桥接、请求处理器、任务完成处理器、库位分配规则）
    /// </summary>
    public static IServiceCollection AddWcsServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 注册 WCS 通信服务
        // ctask 数据库访问（Dapper，独立连接）
        services.AddScoped<ICtaskDbService, CtaskDbService>();

        // 通信适配器（Database / Http 模式）
        var wcsMode = configuration.GetValue<string>("Wcs:Mode") ?? "Database";
        if (wcsMode.Equals("Http", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IWcsTaskBridge, HttpWcsTaskBridge>();
        }
        else
        {
            services.AddScoped<IWcsTaskBridge, DatabaseWcsTaskBridge>();
        }

        // WCS 任务同步服务
        services.AddScoped<WcsTaskSyncService>();

        // WCS 请求处理器（策略模式）
        services.AddScoped<WcsReqHandlers.LocationAllocator>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.InboundRequestHandler>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.InboundEmptyRequestHandler>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.InboundDoubleRequestHandler>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.OutboundRequestHandler>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.MoveRequestHandler>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.WasteDisposalRequestHandler>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.WasteDisposalCaptureRequestHandler>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.VerfiyBatchRequestHandler>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.VerfiyLevelRequestHandler>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.VerfiyProcessRequestHandler>();
        services.AddScoped<WcsReqHandler, WcsReqHandlers.StackingPalletRequestHandler>();

        // 任务完成处理器（策略模式）
        services.AddScoped<ITaskCompletionHandler, TskCompHandlers.InboundCompletionHandler>();
        services.AddScoped<ITaskCompletionHandler, TskCompHandlers.OutboundCompletionHandler>();
        services.AddScoped<ITaskCompletionHandler, TskCompHandlers.MoveCompletionHandler>();

        // 注册库位分配规则（15 条规则 + 策略引擎）
        services.AddSingleton<ILocationAllocationRule, SSRule01>();
        services.AddSingleton<ILocationAllocationRule, SSRule02>();
        services.AddSingleton<ILocationAllocationRule, SSRule03>();
        services.AddSingleton<ILocationAllocationRule, SSRule04>();
        services.AddSingleton<ILocationAllocationRule, SSRule04HcLx>();
        services.AddSingleton<ILocationAllocationRule, SSRule05>();
        services.AddSingleton<ILocationAllocationRule, SSRule06>();
        services.AddSingleton<ILocationAllocationRule, SSRule07>();
        services.AddSingleton<ILocationAllocationRule, SSRule08>();
        services.AddSingleton<ILocationAllocationRule, SSRule09>();
        services.AddSingleton<ILocationAllocationRule, SSRule10>();
        services.AddSingleton<ILocationAllocationRule, SDRule01>();
        services.AddSingleton<ILocationAllocationRule, SDRule02>();
        services.AddSingleton<ILocationAllocationRule, SDRule03>();
        services.AddSingleton<ILocationAllocationRule, SDRule04>();
        services.AddScoped<LocationAllocationEngine>();

        return services;
    }

    /// <summary>
    /// 注册 WCS 通信客户端（Polly 重试 + 熔断）和健康检查
    /// </summary>
    public static IServiceCollection AddWcsClient(this IServiceCollection services, IConfiguration configuration)
    {
        // 添加 HttpClient 用于 WCS 健康检查
        services.AddHttpClient<WcsHealthCheck>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // 配置 WCS 客户端选项
        services.Configure<WcsClientOptions>(
            configuration.GetSection(WcsClientOptions.SectionName));

        // 添加 WCS 通信客户端（Polly 重试 + 熔断）
        services.AddHttpClient<IWcsClient, DefaultWcsClient>(client =>
        {
            var wcsEndpoint = configuration["Wcs:Endpoint"] ?? string.Empty;
            if (!string.IsNullOrEmpty(wcsEndpoint))
            {
                client.BaseAddress = new Uri(wcsEndpoint.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(
                    configuration.GetValue("Wcs:TimeoutSeconds", 10));
            }
        })
        .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: configuration.GetValue("Wcs:RetryCount", 3),
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // 日志在 DefaultWcsClient 中处理
                }))
        .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: configuration.GetValue("Wcs:CircuitBreakerFailureThreshold", 5),
                durationOfBreak: TimeSpan.FromSeconds(configuration.GetValue("Wcs:CircuitBreakerDurationSeconds", 30))));

        return services;
    }
}
