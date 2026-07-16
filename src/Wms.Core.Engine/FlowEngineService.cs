using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Entities.Flow;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Engine;
using Wms.Core.Application.Persistence;
using Wms.Core.Application.Ports;

namespace Wms.Core.Engine;

/// <summary>
/// 流程引擎实现 — 条件匹配器 + Pipeline 执行器 + 模板缓存
/// </summary>
public class FlowEngineService : IFlowEngine
{
    private record CompletionLogData(int InstanceId, string Status, DateTime CompletedTime, string? ErrorMsg, List<FlowNodeLog> Logs);

    private readonly IFlowDbContext _db;
    private readonly IDictionary<string, INodeHandler> _handlers;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FlowEngineService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundTaskQueue _taskQueue;

    /// <summary>
    /// 缓存键前缀
    /// </summary>
    private const string CacheKeyPrefix = "flow:template:";

    /// <summary>
    /// 缓存版本号（static，所有实例共享），递增后旧缓存键自然失效
    /// </summary>
    private static int _cacheVersion = 0;

    public FlowEngineService(
        IFlowDbContext db,
        IEnumerable<INodeHandler> handlers,
        IMemoryCache cache,
        ILogger<FlowEngineService> logger,
        IServiceScopeFactory scopeFactory,
        IBackgroundTaskQueue taskQueue)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _handlers = handlers.ToDictionary(h => h.NodeType);
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _taskQueue = taskQueue ?? throw new ArgumentNullException(nameof(taskQueue));
    }

    /// <summary>
    /// 匹配流程模板（带 IMemoryCache 缓存）
    /// </summary>
    public async Task<FlowTemplate?> MatchTemplateAsync(string requestType, string phase, int? warehouseId = null, string? locationTag = null)
    {
        var cacheKey = $"{CacheKeyPrefix}v{_cacheVersion}:{requestType}:{phase}:{warehouseId}:{locationTag}";
        if (_cache.TryGetValue(cacheKey, out FlowTemplate? cached))
            return cached;

        var query = _db.FlowTemplates
            .Include(t => t.Nodes!.Where(n => !n.IsDeleted).OrderBy(n => n.StepOrder))
            .Where(t => t.Category == requestType && t.Phase == phase && t.IsActive);

        var template = await query
            .OrderByDescending(t => t.Priority)
            .FirstOrDefaultAsync();

        if (template != null)
        {
            _logger.LogInformation("[FlowEngine] 匹配模板: {Name}(Id={Id}), Category={Cat}, Phase={Phase}, 节点数={NodeCount}",
                template.Name, template.Id, requestType, phase, template.Nodes?.Count ?? 0);
            foreach (var n in (template.Nodes ?? []).OrderBy(n => n.StepOrder))
                _logger.LogInformation("[FlowEngine]   节点 Order={O}: {Type} ({Name})", n.StepOrder, n.NodeType, n.NodeName);
        }
        else
        {
            _logger.LogWarning("[FlowEngine] 未匹配到模板: requestType={RequestType}, phase={Phase}", requestType, phase);
        }

        _cache.Set(cacheKey, template, TimeSpan.FromMinutes(5));
        return template;
    }

    /// <summary>
    /// 执行流程（WCS 请求阶段，返回 WcsResult）
    /// </summary>
    public async Task<WcsResult> ExecuteAsync(FlowTemplate template, FlowContext context)
    {
        context.FlowCategory = template.Category;
        var instance = await CreateInstanceAsync(template, context);
        var nodes = template.Nodes!.Where(n => n.IsEnabled && !n.IsDeleted).OrderBy(n => n.StepOrder).ToList();
        var nodeLogs = new List<FlowNodeLog>();

        WcsResult? result = null;

        // ★ 分段事务：开启第一段事务
        await context.UnitOfWork.BeginTransactionAsync();

        foreach (var node in nodes)
        {
            instance.CurrentNodeOrder = node.StepOrder;

            _logger.LogInformation("[FlowEngine] 执行节点: Order={Order}, Type={Type}, Name={Name}, Enabled={Enabled}",
                node.StepOrder, node.NodeType, node.NodeName, node.IsEnabled);

            // 检查跳过条件
            if (!string.IsNullOrEmpty(node.SkipCondition))
            {
                nodeLogs.Add(CreateNodeLog(instance.Id, node, "Skipped"));
                continue;
            }

            if (!_handlers.TryGetValue(node.NodeType, out var handler))
            {
                _logger.LogWarning("[FlowEngine] 未注册的节点类型: {NodeType}", node.NodeType);
                nodeLogs.Add(CreateNodeLog(instance.Id, node, "Skipped", "未注册的节点类型"));
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var nodeResult = await handler.ExecuteAsync(context, node.ConfigJson);
                _logger.LogInformation("[FlowEngine] 节点 {Type} 完成: Success={Success}, Skipped={Skipped}, Stop={Stop}, Msg={Msg}",
                    node.NodeType, nodeResult.Success, nodeResult.Skipped, nodeResult.Stop, nodeResult.Message);

                if (nodeResult.Output != null)
                    context.Merge(nodeResult.Output);

                sw.Stop();
                nodeLogs.Add(CreateNodeLog(instance.Id, node, nodeResult.Skipped ? "Skipped" : "Success",
                    durationMs: sw.ElapsedMilliseconds, outputJson: nodeResult.Output != null ? JsonSerializer.Serialize(nodeResult.Output) : null));

                if (nodeResult.Skipped)
                {
                    if (node.OnFailure == "Stop") break;
                    continue;
                }

                if (!nodeResult.Success)
                {
                    instance.Status = "Failed";
                    instance.ErrorMsg = nodeResult.Message;

                    var resultCode = nodeResult.ResultCode ?? ResultCodeTypes.程序异常;
                    var resultData = nodeResult.ResultData ?? -1;
                    result = ApiResultHelper.WcsFail(nodeResult.Message ?? "节点执行失败",
                        resultCode, resultData);

                    if (node.OnFailure == "Skip") continue;
                    break;
                }

                // ★ 分段事务：boundary 节点处提交当前段并开启新段
                if (node.IsTransactionBoundary)
                {
                    await context.UnitOfWork.CommitAsync();
                    await context.UnitOfWork.BeginTransactionAsync();
                }

                if (nodeResult.Stop) break;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[FlowEngine] 节点 {NodeType} 执行失败: {Message}", node.NodeType, ex.Message);
                nodeLogs.Add(CreateNodeLog(instance.Id, node, "Failed", errorMsg: ex.Message, durationMs: sw.ElapsedMilliseconds));

                instance.Status = "Failed";
                instance.ErrorMsg = ex.Message;
                result = ApiResultHelper.WcsFail($"节点 {node.NodeName} 执行失败: {ex.Message}",
                    ResultCodeTypes.程序异常, -1);

                // ★ 分段事务：回滚当前段
                try { await context.UnitOfWork.RollbackAsync(); }
                catch (Exception rbEx) { _logger.LogWarning(rbEx, "[FlowEngine] Rollback 失败: {Message}", rbEx.Message); }

                // 如果 OnFailure == "Skip"，重新开启新段继续执行
                if (node.OnFailure == "Skip")
                {
                    await context.UnitOfWork.BeginTransactionAsync();
                    continue;
                }
                break;
            }
        }

        // 确定最终状态
        var finalStatus = result == null ? "Completed" : instance.Status;
        var finalErrorMsg = instance.ErrorMsg;
        if (result == null)
        {
            // ★ 分段事务：成功路径，提交最后一段（CommitAsync 内部包含 SaveChanges）
            try
            {
                await context.UnitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FlowEngine] 请求阶段最终 Commit 失败: {Message}", ex.Message);
                instance.Status = "Failed";
                instance.ErrorMsg = ex.Message;
                result = ApiResultHelper.WcsFail("服务器内部错误", ResultCodeTypes.程序异常, -1);
            }
            result ??= ApiResultHelper.WcsSuccess("处理成功", ResultCodeTypes.一, 1);
        }
        else
        {
            // ★ 分段事务：失败路径，回滚未提交的当前段
            try { await context.UnitOfWork.RollbackAsync(); }
            catch (Exception rbEx) { _logger.LogWarning(rbEx, "[FlowEngine] 失败路径 Rollback 忽略: {Message}", rbEx.Message); }
        }

        // ★ 异步保存实例最终状态 + 节点日志（不影响主流程性能）
        var completedTime = DateTime.Now;
        var instanceId = instance.Id;
        _ = _taskQueue.QueueAsync(_ => SaveFlowDataAsync(instanceId, finalStatus, completedTime, finalErrorMsg, nodeLogs));

        return result;
    }

    /// <summary>
    /// 执行流程（任务完成阶段，无返回值）
    /// </summary>
    /// <remarks>
    /// 两阶段执行：
    /// Phase 1（事务内）：常规节点（!IsPostTransaction）
    /// Phase 2（事务外）：事务后节点（IsPostTransaction）— MES 上传、杭可通知等外部调用
    /// </remarks>
    public async Task ExecuteCompletionAsync(FlowTemplate template, FlowContext context)
    {
        context.FlowCategory = template.Category;
        CompletionLogData logData;

        // ★ Phase 1：事务内执行常规节点
        await using var transaction = await context.Db.Database.BeginTransactionAsync();
        try
        {
            logData = await ExecuteCompletionNodesAsync(template, context, postTransactionOnly: false);
            await transaction.CommitAsync();

            // ★ Commit 成功后才异步保存日志（避免 Rollback 时保存孤儿日志）
            _ = _taskQueue.QueueAsync(_ => SaveFlowDataAsync(
                logData.InstanceId, logData.Status, logData.CompletedTime, logData.ErrorMsg, logData.Logs));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw; // 事务失败不执行 Phase 2
        }

        // ★ Phase 2：事务外执行事务后节点（仅当 Phase 1 commit 成功时）
        await ExecutePostTransactionNodesAsync(template, context, logData);
    }

    /// <summary>
    /// 完成阶段节点执行逻辑（支持按 IsPostTransaction 过滤）
    /// </summary>
    private async Task<CompletionLogData> ExecuteCompletionNodesAsync(FlowTemplate template, FlowContext context, bool postTransactionOnly)
    {
        var instance = await CreateInstanceAsync(template, context);
        var nodes = template.Nodes!
            .Where(n => n.IsEnabled && !n.IsDeleted && n.IsPostTransaction == postTransactionOnly)
            .OrderBy(n => n.StepOrder)
            .ToList();
        var nodeLogs = new List<FlowNodeLog>();

        foreach (var node in nodes)
        {
            instance.CurrentNodeOrder = node.StepOrder;

            _logger.LogInformation("[FlowEngine] {Phase}执行节点: Order={Order}, Type={Type}, Name={Name}",
                postTransactionOnly ? "事务后" : "完成阶段", node.StepOrder, node.NodeType, node.NodeName);

            if (!string.IsNullOrEmpty(node.SkipCondition))
            {
                nodeLogs.Add(CreateNodeLog(instance.Id, node, "Skipped"));
                continue;
            }

            if (!_handlers.TryGetValue(node.NodeType, out var handler))
            {
                nodeLogs.Add(CreateNodeLog(instance.Id, node, "Skipped", "未注册的节点类型"));
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var nodeResult = await handler.ExecuteAsync(context, node.ConfigJson);

                _logger.LogInformation("[FlowEngine] {Phase}节点 {Type} 完成: Success={Success}, Skipped={Skipped}, Msg={Msg}",
                    postTransactionOnly ? "事务后" : "完成阶段", node.NodeType, nodeResult.Success, nodeResult.Skipped, nodeResult.Message);

                if (nodeResult.Output != null)
                    context.Merge(nodeResult.Output);

                sw.Stop();
                nodeLogs.Add(CreateNodeLog(instance.Id, node, nodeResult.Skipped ? "Skipped" : "Success",
                    durationMs: sw.ElapsedMilliseconds));

                if (nodeResult.Skipped)
                {
                    if (node.OnFailure == "Stop") break;
                    continue;
                }

                if (!nodeResult.Success)
                {
                    instance.Status = "Failed";
                    instance.ErrorMsg = nodeResult.Message;
                    _logger.LogError("[FlowEngine] {Phase}节点 {NodeType} 失败: {Message}",
                        postTransactionOnly ? "事务后" : "完成阶段", node.NodeType, nodeResult.Message);
                    if (node.OnFailure == "Skip") continue;
                    break;
                }

                // 事务断点（仅事务内节点生效）
                if (!postTransactionOnly && node.IsTransactionBoundary)
                {
                    await context.Db.SaveChangesAsync();
                }

                if (nodeResult.Stop) break;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[FlowEngine] {Phase}节点 {NodeType} 异常: {Message}",
                    postTransactionOnly ? "事务后" : "完成阶段", node.NodeType, ex.Message);
                nodeLogs.Add(CreateNodeLog(instance.Id, node, "Failed", errorMsg: ex.Message, durationMs: sw.ElapsedMilliseconds));
                instance.Status = "Failed";
                instance.ErrorMsg = ex.Message;
                throw; // 完成阶段异常需要向上抛出
            }
        }

        // ★ 最终 SaveChanges（仅事务内节点阶段执行）
        if (!postTransactionOnly)
        {
            try
            {
                await context.Db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FlowEngine] 完成阶段最终 SaveChanges 失败: {Message}", ex.Message);
                instance.Status = "Failed";
                instance.ErrorMsg = ex.Message;
                throw; // 让 ExecuteCompletionAsync 触发事务回滚
            }
        }

        if (instance.Status != "Failed")
            instance.Status = "Completed";

        return new CompletionLogData(instance.Id, instance.Status, DateTime.Now, instance.ErrorMsg, nodeLogs);
    }

    /// <summary>
    /// 事务后节点执行（Phase 2）— 每个 Try-Catch 独立，永不向上抛异常
    /// </summary>
    private async Task ExecutePostTransactionNodesAsync(FlowTemplate template, FlowContext context, CompletionLogData logData)
    {
        var postNodes = template.Nodes!
            .Where(n => n.IsEnabled && n.IsPostTransaction)
            .OrderBy(n => n.StepOrder)
            .ToList();

        if (postNodes.Count == 0) return;

        var postNodeLogs = new List<FlowNodeLog>();

        foreach (var node in postNodes)
        {
            if (!_handlers.TryGetValue(node.NodeType, out var handler))
            {
                _logger.LogWarning("[FlowEngine] 事务后节点未注册: {NodeType}", node.NodeType);
                postNodeLogs.Add(CreateNodeLog(logData.InstanceId, node, "Skipped", "未注册的节点类型"));
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var nodeResult = await handler.ExecuteAsync(context, node.ConfigJson);

                _logger.LogInformation("[FlowEngine] 事务后节点 {Type} 完成: Success={Success}, Skipped={Skipped}, Msg={Msg}",
                    node.NodeType, nodeResult.Success, nodeResult.Skipped, nodeResult.Message);

                if (nodeResult.Output != null)
                    context.Merge(nodeResult.Output);

                sw.Stop();
                postNodeLogs.Add(CreateNodeLog(logData.InstanceId, node, nodeResult.Skipped ? "Skipped" : "Success",
                    durationMs: sw.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[FlowEngine] 事务后节点 {NodeType} 异常（不影响主流程）: {Message}",
                    node.NodeType, ex.Message);
                postNodeLogs.Add(CreateNodeLog(logData.InstanceId, node, "Failed", errorMsg: ex.Message, durationMs: sw.ElapsedMilliseconds));
            }
        }

        // ★ 追加异步保存事务后节点日志
        if (postNodeLogs.Count > 0)
        {
            _ = _taskQueue.QueueAsync(_ => SaveFlowDataAsync(
                logData.InstanceId, logData.Status, logData.CompletedTime, logData.ErrorMsg, postNodeLogs));
        }
    }

    /// <summary>
    /// 清除模板缓存
    /// </summary>
    public void ClearCache()
    {
        Interlocked.Increment(ref _cacheVersion);
        _logger.LogInformation("[FlowEngine] 模板缓存已清除 (version → {Version})", _cacheVersion);
    }

    /// <summary>
    /// 创建流程实例并持久化（获取自增 Id 供节点日志关联）
    /// </summary>
    private async Task<FlowInstance> CreateInstanceAsync(FlowTemplate template, FlowContext context)
    {
        var instance = new FlowInstance
        {
            InstanceCode = Guid.NewGuid().ToString("N")[..12],
            TemplateId = template.Id,
            BusinessType = context.BusinessType ?? "Unknown",
            BusinessId = context.BusinessId ?? context.WcsRequest?.LocationCode ?? context.WcsTask?.TaskCode,
            Status = "Running",
            CurrentNodeOrder = 0,
            CreatedTime = DateTime.Now
        };
        context.Db.FlowInstances.Add(instance);
        await context.Db.SaveChangesAsync(); // 获取自增 Id
        return instance;
    }

    private static FlowNodeLog CreateNodeLog(int instanceId, FlowNode node, string status,
        string? errorMsg = null, long? durationMs = null, string? outputJson = null)
    {
        return new FlowNodeLog
        {
            InstanceId = instanceId,
            NodeOrder = node.StepOrder,
            NodeType = node.NodeType,
            NodeName = node.NodeName,
            Status = status,
            DurationMs = durationMs ?? 0,
            ErrorMsg = errorMsg,
            OutputJson = outputJson,
            CreatedTime = DateTime.Now
        };
    }

    /// <summary>
    /// 异步保存实例最终状态 + 节点日志（使用独立 scope 避免主请求结束后 DbContext 被释放）
    /// </summary>
    private async Task SaveFlowDataAsync(int instanceId, string status, DateTime completedTime, string? errorMsg, List<FlowNodeLog> logs)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IFlowDbContext>();

            // 更新实例最终状态
            var instance = await db.FlowInstances.FindAsync(instanceId);
            if (instance != null)
            {
                instance.Status = status;
                instance.CompletedTime = completedTime;
                instance.ErrorMsg = errorMsg;
            }

            // 写入节点日志
            if (logs.Count > 0)
                db.FlowNodeLogs.AddRange(logs);

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FlowEngine] 异步保存流程数据失败: {Message}", ex.Message);
        }
    }
}
