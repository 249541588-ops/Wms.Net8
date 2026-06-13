using Microsoft.AspNetCore.SignalR;

namespace Wms.Core.WebApi.Hubs;

/// <summary>
/// WMS 实时通信 Hub
/// </summary>
/// <remarks>
/// 用于向前端推送任务状态、库存变更、告警等实时消息。
/// 后端 Service 通过注入 <see cref="IHubContext{WmsHub}"/> 发送消息。
/// </remarks>
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

    // === 后端调用方法（通过 IHubContext<WmsHub> 调用，不暴露给客户端） ===

    /// <summary>
    /// 推送任务状态更新
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="status">任务状态</param>
    public Task SendTaskUpdate(string taskId, string status)
    {
        return Clients.All.SendAsync("ReceiveTaskUpdate", new { taskId, status, timestamp = DateTime.Now });
    }

    /// <summary>
    /// 推送库存变更通知
    /// </summary>
    /// <param name="locationId">库位ID</param>
    /// <param name="materialId">物料ID</param>
    /// <param name="qty">变更数量</param>
    public Task SendStockChange(int locationId, int materialId, decimal qty)
    {
        return Clients.All.SendAsync("ReceiveStockChange", new { locationId, materialId, qty, timestamp = DateTime.Now });
    }

    /// <summary>
    /// 推送告警消息
    /// </summary>
    /// <param name="message">告警内容</param>
    /// <param name="level">告警级别（info/warning/error）</param>
    public Task SendAlert(string message, string level)
    {
        return Clients.All.SendAsync("ReceiveAlert", new { message, level, timestamp = DateTime.Now });
    }
}
