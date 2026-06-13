using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Services;
using Wms.Core.Domain.Factories;
using Wms.Core.Domain.Entities.Flow;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Infrastructure.Persistence.Repositories;
using Wms.Core.Infrastructure.Services;
using Wms.Core.Engine;
using Wms.Core.Engine.Nodes;

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
        services.AddDbContext<WmsDbContext>(options =>
            options.UseSqlServer(connectionString));

        // 注册独立日志数据库上下文（InterfaceLogs，避免日志增长影响主库）
        var logConnectionString = configuration.GetConnectionString("LogConnection");
        if (!string.IsNullOrEmpty(logConnectionString))
        {
            services.AddDbContext<WmsLogDbContext>(options =>
                options.UseSqlServer(logConnectionString));
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

        services.AddScoped<IBasicDictionaryService, BasicDictionaryService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IOutboundTimerService, OutboundTimerService>();
        services.AddScoped<IBatteryCellSortingService, BatteryCellSortingService>();
        services.AddScoped<IBatteryCellService, BatteryCellService>();

        // 注册辅助服务（Singleton - 全局单例）
        services.AddSingleton<IContainerCodeValidator, ContainerCodeValidator>();
        services.AddSingleton<EntityFactory>();

        // 注册流程引擎（Scoped - 每个请求一个实例）
        services.AddScoped<IFlowEngine, FlowEngineService>();

        // 注册所有节点处理器
        services.AddScoped<INodeHandler, ValidateParamsHandler>();
        services.AddScoped<INodeHandler, FindUnitloadHandler>();
        services.AddScoped<INodeHandler, CheckUnitloadStatusHandler>();
        services.AddScoped<INodeHandler, MatchTagHandler>();
        services.AddScoped<INodeHandler, AllocateLocationHandler>();
        services.AddScoped<INodeHandler, CheckLocationLimitHandler>();
        services.AddScoped<INodeHandler, CreateTransTaskHandler>();
        services.AddScoped<INodeHandler, SendWcsTaskHandler>();
        services.AddScoped<INodeHandler, UpdateUnitloadHandler>();
        services.AddScoped<INodeHandler, UpdateLocationCountHandler>();
        services.AddScoped<INodeHandler, RecordFlowHandler>();
        services.AddScoped<INodeHandler, ArchiveTaskHandler>();
        services.AddScoped<INodeHandler, SplitUnitloadHandler>();
        services.AddScoped<INodeHandler, AdvanceOperationHandler>();
        services.AddScoped<INodeHandler, HttpCallbackHandler>();

        return services;
    }
}
