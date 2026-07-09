using Polly;
using Wms.Core.Application.Ports;
using Wms.Core.Infrastructure.Clients;

namespace Wms.Core.WebApi.Extensions;

/// <summary>
/// MES / 杭可客户端 DI 注册扩展
/// </summary>
public static class MesExtensions
{
    /// <summary>
    /// 注册 MES 通信客户端（Polly 重试 + 熔断）
    /// </summary>
    public static IServiceCollection AddMesClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MesClientOptions>(
            configuration.GetSection(MesClientOptions.SectionName));

        services.AddHttpClient<IMesClient, DefaultMesClient>(client =>
        {
            var mesEndpoint = configuration["Mes:Endpoint"] ?? string.Empty;
            if (!string.IsNullOrEmpty(mesEndpoint))
            {
                client.BaseAddress = new Uri(mesEndpoint.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(
                    configuration.GetValue("Mes:TimeoutSeconds", 10));
            }
        })
        .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: configuration.GetValue("Mes:RetryCount", 3),
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) => { }))
        .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: configuration.GetValue("Mes:CircuitBreakerFailureThreshold", 5),
                durationOfBreak: TimeSpan.FromSeconds(configuration.GetValue("Mes:CircuitBreakerDurationSeconds", 30))));

        return services;
    }

    /// <summary>
    /// 注册杭可通信客户端（Polly 重试 + 熔断）
    /// </summary>
    public static IServiceCollection AddHangKeClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HangKeClientOptions>(
            configuration.GetSection(HangKeClientOptions.SectionName));

        services.AddHttpClient<IHangKeClient, DefaultHangKeClient>(client =>
        {
            var hangKeEndpoint = configuration["HangKe:Endpoint"] ?? string.Empty;
            if (!string.IsNullOrEmpty(hangKeEndpoint))
            {
                client.BaseAddress = new Uri(hangKeEndpoint.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(
                    configuration.GetValue("HangKe:TimeoutSeconds", 10));
            }
        })
        .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: configuration.GetValue("HangKe:RetryCount", 3),
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) => { }))
        .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: configuration.GetValue("HangKe:CircuitBreakerFailureThreshold", 5),
                durationOfBreak: TimeSpan.FromSeconds(configuration.GetValue("HangKe:CircuitBreakerDurationSeconds", 30))));

        return services;
    }
}
