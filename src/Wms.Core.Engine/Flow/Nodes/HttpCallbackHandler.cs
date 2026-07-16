using Microsoft.Extensions.Logging;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// HTTP 回调节点（预留）— 调用 MES/第三方系统
/// </summary>
public class HttpCallbackHandler : INodeHandler
{
    public string NodeType => "HttpCallback";
    public string DisplayName => "HTTP回调";
    public string Category => "外部交互";
    public string Description => "向外部系统（MES 等）发送 HTTP 回调通知";
    public string? ConfigSchema => """
    {
      "type": "object",
      "properties": {
        "url": { "type": "string", "description": "回调 URL" },
        "method": { "type": "string", "enum": ["GET", "POST"], "default": "POST" },
        "timeout": { "type": "integer", "description": "超时毫秒", "default": 5000 }
      }
    }
    """;

    private readonly ILogger<HttpCallbackHandler> _logger;

    public HttpCallbackHandler(ILogger<HttpCallbackHandler> logger)
    {
        _logger = logger;
    }

    public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        // 预留实现 — 当前直接跳过
        _logger.LogInformation("[FlowNode:HttpCallback] HTTP 回调节点（预留，未实现）");
        return Task.FromResult(NodeResult.Skip("HTTP 回调节点尚未实现"));
    }
}
