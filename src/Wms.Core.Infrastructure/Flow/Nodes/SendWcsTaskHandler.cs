using Microsoft.Extensions.Logging;
using Wms.Core.Engine;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 下发 WCS 任务节点 — 调用 IWcsTaskBridge.SendTaskAsync
/// </summary>
public class SendWcsTaskHandler : INodeHandler
{
    public string NodeType => "SendWcsTask";
    public string DisplayName => "下发WCS";
    public string Category => "外部交互";
    public string Description => "将运输任务下发给 WCS 系统";
    public string? ConfigSchema => null;

    private readonly IWcsTaskBridge _wcsBridge;
    private readonly ILogger<SendWcsTaskHandler> _logger;

    public SendWcsTaskHandler(IWcsTaskBridge wcsBridge, ILogger<SendWcsTaskHandler> logger)
    {
        _wcsBridge = wcsBridge ?? throw new ArgumentNullException(nameof(wcsBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var transTask = context.TransTask;
        if (transTask == null)
            return NodeResult.Fail("无运输任务可下发");

        await _wcsBridge.SendTaskAsync(transTask);
        transTask.WasSentToWcs = true;
        transTask.SentToWcsAt = DateTime.Now;

        _logger.LogInformation("[FlowNode:SendWcsTask] 任务已下发: TaskCode={TaskCode}", transTask.TaskCode);

        return NodeResult.Ok();
    }
}
