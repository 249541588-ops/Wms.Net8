using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Enums;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 通知杭可节点 — 完成阶段事务后调用杭可设备出入口通知
/// </summary>
/// <remarks>
/// 仅对化成分容柜对应库区（L1/L4/L5/L6）生效
/// 入库成功后禁入库位，出库成功后解除禁出
/// </remarks>
public class NotifyHangKeHandler : INodeHandler
{
    public string NodeType => "NotifyHangKe";
    public string DisplayName => "通知杭可";
    public string Category => "外部交互";
    public string Description => "向杭可设备发送出入口通知（事务后执行，失败不影响主流程）";
    public string? ConfigSchema => null;

    private readonly IHangKeClient _hangkeClient;
    private readonly HangKeClientOptions _hangkeOptions;
    private readonly ILogger<NotifyHangKeHandler> _logger;

    public NotifyHangKeHandler(
        IHangKeClient hangkeClient,
        IOptions<HangKeClientOptions> hangkeOptions,
        ILogger<NotifyHangKeHandler> logger)
    {
        _hangkeClient = hangkeClient ?? throw new ArgumentNullException(nameof(hangkeClient));
        _hangkeOptions = hangkeOptions?.Value ?? throw new ArgumentNullException(nameof(hangkeOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        // 未启用杭可通知 → 跳过
        if (!_hangkeOptions.Enable)
            return NodeResult.Skip("杭可通知未启用");

        // 取消时不通知
        if (context.IsCancelled)
            return NodeResult.Skip("任务已取消，跳过杭可通知");

        var isInbound = context.FlowCategory == Cst.入库 || context.FlowCategory == Cst.入库双叉;
        var unitload = context.Unitload;

        if (unitload == null || string.IsNullOrWhiteSpace(unitload.ContainerCode))
        {
            _logger.LogWarning("[FlowNode:NotifyHangKe] 托盘为空或条码为空，跳过杭可通知: BusinessId={BusinessId}", context.BusinessId);
            return NodeResult.Skip("托盘为空或条码为空");
        }

        // 确定目标 location：入库用终点，出库用起点
        var location = isInbound ? context.TargetLocation : context.StartLocation;
        if (location == null)
        {
            _logger.LogWarning("[FlowNode:NotifyHangKe] 库位为空，跳过杭可通知: BusinessId={BusinessId}", context.BusinessId);
            return NodeResult.Skip("库位为空");
        }

        // 显式加载 Rack.Laneway 导航属性（WcsTaskSyncService 未 Include 这些）
        try
        {
            await context.Db.Entry(location).Reference(l => l.Rack).LoadAsync();
            if (location.Rack != null)
                await context.Db.Entry(location.Rack).Reference(r => r.Laneway).LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FlowNode:NotifyHangKe] 加载库位导航属性失败，跳过: BusinessId={BusinessId}", context.BusinessId);
            return NodeResult.Skip("加载导航属性失败");
        }

        var lanewayCode = location.Rack?.Laneway?.LanewayCode;
        if (string.IsNullOrEmpty(lanewayCode) || !CommonTypes.化成分容柜对应库区.Contains(lanewayCode))
        {
            return NodeResult.Skip($"库位不在化成分容柜对应库区: LanewayCode={lanewayCode}");
        }

        var containerCode = unitload.ContainerCode;
        var inOutType = isInbound ? InOutType_Enum.入库 : InOutType_Enum.出库;

        try
        {
            var result = await _hangkeClient.InOutNotifyAsync(
                location.AnotherCode ?? "", containerCode, inOutType);

            if (result.ResultCode == 1)
            {
                _logger.LogInformation("[FlowNode:NotifyHangKe] 托盘 {ContainerCode} 杭可{Biz}通知成功: 库位={LocCode}, ResultCode={Code}",
                    containerCode, isInbound ? "入库" : "出库", location.LocationCode, result.ResultCode);

                // 成功后更新 location 状态
                if (isInbound)
                {
                    location.InboundDisabled = true;
                    location.InboundDisabledComment = $"{containerCode} 入库完成,禁入";
                }
                else
                {
                    location.OutboundDisabled = true;
                    location.OutboundDisabledComment = $"{containerCode} 出库完成，禁出";
                    location.HKPosintionCK = 0;
                }

                await context.Db.SaveChangesAsync();
            }
            else
            {
                _logger.LogWarning("[FlowNode:NotifyHangKe] 托盘 {ContainerCode} 杭可{Biz}通知失败: 库位={LocCode}, ResultCode={Code}, 原因={Msg}",
                    containerCode, isInbound ? "入库" : "出库", location.LocationCode, result.ResultCode, result.ResultMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FlowNode:NotifyHangKe] 杭可通知异常: BusinessId={BusinessId}", context.BusinessId);
            return NodeResult.Skip("杭可通知异常");
        }

        return NodeResult.Ok();
    }
}
