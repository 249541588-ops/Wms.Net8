using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Handlers.WcsRequest;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 归档任务节点 — TransTask → ArchivedTask
/// </summary>
public class ArchiveTaskHandler : INodeHandler
{
    public string NodeType => "ArchiveTask";
    public string DisplayName => "归档任务";
    public string Category => "业务逻辑";
    public string Description => "将 TransTask 归档到 ArchivedTasks 表，并删除原 TransTask";
    public string? ConfigSchema => null;

    private readonly ILogger<ArchiveTaskHandler> _logger;

    public ArchiveTaskHandler(ILogger<ArchiveTaskHandler> logger)
    {
        _logger = logger;
    }

    public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var transTask = context.TransTask;
        if (transTask == null)
            return Task.FromResult(NodeResult.Skip("无运输任务可归档"));

        var isCancelled = context.IsCancelled;
        var actualLocationCode = context.WcsTask?.ActEndLoc ?? context.WcsTask?.EndLoc;
        var isCompletion = context.Phase == Cst.PhaseCompletion;

        if (isCompletion)
        {
            LocationAllocator.ArchiveTask(context.Db, transTask, actualLocationCode, isCancelled);
        }

        return Task.FromResult(NodeResult.Ok());
    }
}
