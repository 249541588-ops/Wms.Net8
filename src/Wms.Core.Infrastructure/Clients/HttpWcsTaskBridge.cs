using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Infrastructure.Clients;

/// <summary>
/// WCS 任务通信适配器 — HTTP API 模式（后期实现）
/// </summary>
public class HttpWcsTaskBridge : IWcsTaskBridge
{
    private readonly ILogger<HttpWcsTaskBridge> _logger;

    public HttpWcsTaskBridge(ILogger<HttpWcsTaskBridge> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendTaskAsync(TransTask transTask)
    {
        _logger.LogWarning("[WcsBridge-Http] SendTaskAsync 尚未实现");
        throw new NotImplementedException("HTTP 模式 WCS 通信尚未实现，请切换到 Database 模式");
    }

    /// <summary>
    /// 轮询 WCS 任务状态变更（HTTP API，待实现）
    /// </summary>
    public Task<IReadOnlyList<WcsTask>> PollStatusChangesAsync()
    {
        _logger.LogWarning("[WcsBridge-Http] PollStatusChangesAsync 尚未实现");
        throw new NotImplementedException("HTTP 模式 WCS 通信尚未实现，请切换到 Database 模式");
    }
}
