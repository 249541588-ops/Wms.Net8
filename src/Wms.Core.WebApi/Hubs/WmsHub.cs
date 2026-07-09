using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Wms.Core.WebApi.Hubs;

/// <summary>
/// WMS 实时通信 Hub
/// </summary>
/// <remarks>
/// 连接需通过 JWT 鉴权（类级 <see cref="AuthorizeAttribute"/>）。
/// 后端 Service 通过注入 <see cref="IHubContext{WmsHub}"/> 直接调用
/// <c>Clients.All.SendAsync(eventName, payload)</c> 推送消息；
/// 本 Hub 不向客户端暴露任何可调用方法。
/// </remarks>
[Authorize]
public class WmsHub : Hub
{
    /// <summary>
    /// 客户端连接时调用
    /// </summary>
    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开时调用
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        return base.OnDisconnectedAsync(exception);
    }
}
