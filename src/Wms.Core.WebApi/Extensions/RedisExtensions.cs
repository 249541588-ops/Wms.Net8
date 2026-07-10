using StackExchange.Redis;
using Wms.Core.Application.Ports;
using Wms.Core.Application.Services;
using Wms.Core.Infrastructure.Caching;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Services;
using Wms.Core.Infrastructure.Services.ReportProviders;
using Wms.Core.WebApi.Configuration;
using Wms.Core.WebApi.Filters;
using Wms.Core.WebApi.Helpers;
using Wms.Core.WebApi.Middleware;
using Wms.Core.WebApi.Services;

namespace Wms.Core.WebApi.Extensions;

public static class RedisExtensions
{
    /// <summary>
    /// 配置 Redis 分布式缓存、SignalR 实时通信
    /// </summary>
    public static IServiceCollection AddWmsRedis(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置 Redis 分布式缓存
        var redisSection = configuration.GetSection(RedisOptions.SectionName);
        var redisEnabled = redisSection.GetValue<bool>("Enabled");
        var redisConnectionString = redisSection["ConnectionString"];

        if (redisEnabled && !string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = redisSection.GetValue<string>("InstanceName") ?? "Wms:";
            });

            Console.WriteLine("Redis distributed cache enabled.");

            // 注册 IConnectionMultiplexer（单例）供 InventoryCacheService 和 DistributedLockService 使用
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString));

            // 注册库存缓存和分布式锁服务
            services.AddScoped<IInventoryCacheService, InventoryCacheService>();
            services.AddScoped<IDistributedLockService, DistributedLockService>();

            // 多实例部署：SignalR Redis Backplane（实时消息跨实例同步）
            services.AddSignalR()
                .AddStackExchangeRedis(redisConnectionString, options =>
                {
                    options.Configuration.ChannelPrefix = redisSection.GetValue<string>("InstanceName") ?? "Wms:";
                });
        }
        else
        {
            // 如果 Redis 未配置，使用内存分布式缓存作为后备
            services.AddDistributedMemoryCache();
            Console.WriteLine("Using in-memory distributed cache (Redis not configured).");

            // 单机部署：SignalR 不需要 Backplane
            services.AddSignalR();
        }

        // 配置 Redis 选项
        services.Configure<RedisOptions>(
            configuration.GetSection(RedisOptions.SectionName));

        // 注册缓存服务（单机用 MemoryCacheService，Redis 启用时用 DistributedCacheService）
        if (redisEnabled && !string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddScoped<ICacheService, DistributedCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        return services;
    }

    /// <summary>
    /// 注册基础应用服务（Token、导出、Dapper、翻译、DbInitializer、全局异常处理等）
    /// </summary>
    public static IServiceCollection AddWmsServices(this IServiceCollection services)
    {
        // 注册 Token 服务
        services.AddScoped<ITokenService, TokenService>();

        // R503: JWT 黑名单服务（依赖 IDistributedCache，Redis 启用时多实例共享，
        // 否则回退到内存缓存仅单实例生效）
        services.AddScoped<IJwtBlacklistService, JwtBlacklistService>();

        // 注册 Excel 服务
        //services.AddScoped<IExcelService, ExcelService>();

        // 注册导出服务
        services.AddScoped<IExportService, ExportService>();

        // 注册 Dapper 高频读取服务（条码扫描、库存查询等场景）
        services.AddScoped<IDapperReadService, DapperReadService>();

        // 注册数据库初始化服务（Scoped，因为依赖 WmsDbContext）
        services.AddScoped<DbInitializer>();

        // 注册语言包缓存键跟踪器（单例，全局共享）
        services.AddSingleton<ILanguagePackCacheTracker, LanguagePackCacheTracker>();

        // 注册翻译服务
        services.AddScoped<ITranslationService, TranslationService>();

        // 注册全局异常处理中间件
        services.AddScoped<GlobalExceptionHandler>();

        // 注册全局操作日志过滤器
        services.AddScoped<OperationLogFilter>();

        // 注册报表服务
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<ISqlValidator, SqlValidator>();
        services.AddScoped<DynamicSqlProvider>();
        // 注册预置报表 Provider
        services.AddScoped<IReportQueryProvider, StockStatisticsProvider>();
        services.AddScoped<IReportQueryProvider, InOutStatisticsProvider>();
        services.AddScoped<IReportQueryProvider, AvailableStockProvider>();
        services.AddScoped<IReportQueryProvider, LocationUsageProvider>();
        services.AddScoped<IReportQueryProvider, GradedStockProvider>();

        return services;
    }
}
