using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Services;
using Wms.Core.Application.Ports;
using Wms.Core.Application.Persistence;
using Wms.Core.Domain.Abstractions;
using Wms.Core.Domain.Factories;
using Wms.Core.Domain.Entities.Flow;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Persistence.Repositories;
using Wms.Core.Infrastructure.Services;

namespace Wms.Core.Infrastructure.DependencyInjection;

/// <summary>
/// 服务集合扩展 - 配置 WMS Core 服务
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 WMS Core 基础设施服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddWmsCoreInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册 EF Core DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContextPool<WmsDbContext>(options =>
            options.UseSqlServer(connectionString), poolSize: 128);

        // 暴露 WmsDbContext 的接口视图，供 FlowContext / 节点处理器 / 事务管理使用
        // 同一 Scope 内 GetRequiredService<WmsDbContext> 返回同一实例，保证 FlowContext.Db 与 UnitOfWork 指向同一 DbContext
        services.AddScoped<IFlowDbContext>(sp => sp.GetRequiredService<WmsDbContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<WmsDbContext>());

        // 注册独立日志数据库上下文（InterfaceLogs，避免日志增长影响主库）
        var logConnectionString = configuration.GetConnectionString("LogConnection");
        if (!string.IsNullOrEmpty(logConnectionString))
        {
            services.AddDbContextPool<WmsLogDbContext>(options =>
                options.UseSqlServer(logConnectionString), poolSize: 32);
        }

        // 注册仓储（Scoped - 每个请求一个仓储实例）
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IBasicDictionaryRepository, BasicDictionaryRepository>();
        services.AddScoped<ICommonRepository, CommonRepository>();

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // 通用仓储（所有实体都需要注册）
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));

        // 注册领域服务（Scoped - 每个请求一个服务实例）
        services.AddScoped<IPortService, PortService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IUnitloadService, UnitloadService>();
        services.AddScoped<IProcessRouteService, ProcessRouteService>();

        services.AddScoped<IBasicDictionaryService, BasicDictionaryService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IOutboundTimerService, OutboundTimerService>();
        services.AddScoped<IBatteryCellSortingService, BatteryCellSortingService>();
        services.AddScoped<IBatteryCellService, BatteryCellService>();

        // 注册辅助服务（Singleton - 全局单例）
        services.AddSingleton<IContainerCodeValidator, ContainerCodeValidator>();
        services.AddSingleton<EntityFactory>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // 后台任务队列（从 WebApi 层移入，基于 Channel 的有序消费）
        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        services.AddHostedService<BackgroundTaskQueueHostedService>();

        // FlowEngine、节点处理器、库位分配规则已迁移到 Wms.Core.Engine 项目
        // 注册方式：services.AddWmsEngine(opt => opt.AddLocationRule<SSRule04HcLx>())
        // 调用点：WebApi 启动代码（Program.cs / WcsExtensions.AddWcsServices）

        return services;
    }
}
