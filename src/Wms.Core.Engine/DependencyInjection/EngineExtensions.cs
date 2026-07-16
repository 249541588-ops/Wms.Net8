using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wms.Core.Application.Persistence;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Tasks;
using Wms.Core.Infrastructure.Handlers.WcsRequest;
using Wms.Core.Infrastructure.Tasks.Rules;

namespace Wms.Core.Engine;

/// <summary>
/// Engine 层 DI 注册扩展方法
/// </summary>
/// <remarks>
/// 使用方式：<code>services.AddWmsEngine(opt => opt.AddLocationRule&lt;SSRule04HcLx&gt;());</code>
/// 客户特定规则（如 SSRule04HcLx）留在项目层 Infrastructure，通过 EngineOptions 注册。
/// </remarks>
public static class EngineExtensions
{
    /// <summary>
    /// 注册 WMS Engine 服务：流程引擎、节点处理器、库位分配器、通用规则
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">Engine 选项配置（可选，用于注册客户特定规则）</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddWmsEngine(
        this IServiceCollection services,
        Action<EngineOptions>? configure = null)
    {
        // 1. 流程引擎
        services.AddScoped<IFlowEngine, FlowEngineService>();

        // 2. 节点处理器（程序集扫描注册所有 INodeHandler 实现）
        var assembly = typeof(EngineExtensions).Assembly;
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(INodeHandler).IsAssignableFrom(t));
        foreach (var type in handlerTypes)
        {
            services.AddScoped(typeof(INodeHandler), type);
        }

        // 3. 库位分配器（节点辅助类，被 AllocateLocationHandler 等注入）
        services.AddScoped<LocationAllocator>();

        // 4. 库位分配引擎（Domain.Tasks，通用）
        services.AddScoped<LocationAllocationEngine>();

        // 5. 通用库位分配规则（程序集扫描注册 Engine 内所有 ILocationAllocationRule 实现）
        //    SSRule04HcLx 等客户特定规则不在 Engine 程序集，不会被扫描到
        var ruleTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(ILocationAllocationRule).IsAssignableFrom(t));
        foreach (var type in ruleTypes)
        {
            services.AddSingleton(typeof(ILocationAllocationRule), type);
        }

        // 6. 客户特定规则（通过 EngineOptions 注册）
        var options = new EngineOptions();
        configure?.Invoke(options);
        foreach (var ruleType in options.AdditionalLocationRules)
        {
            services.AddSingleton(typeof(ILocationAllocationRule), ruleType);
        }

        return services;
    }
}

/// <summary>
/// Engine 选项 — 用于项目层注册客户特定规则
/// </summary>
public class EngineOptions
{
    /// <summary>
    /// 客户特定的库位分配规则类型列表
    /// </summary>
    public List<Type> AdditionalLocationRules { get; } = new();

    /// <summary>
    /// 添加客户特定的库位分配规则
    /// </summary>
    /// <typeparam name="T">规则类型，必须实现 ILocationAllocationRule</typeparam>
    /// <returns>当前选项实例（链式调用）</returns>
    public EngineOptions AddLocationRule<T>() where T : ILocationAllocationRule
    {
        AdditionalLocationRules.Add(typeof(T));
        return this;
    }
}
