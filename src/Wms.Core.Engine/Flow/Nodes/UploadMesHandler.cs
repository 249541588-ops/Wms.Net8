using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 上传 MES 节点 — 完成阶段事务后调用 MES 保存出入库信息
/// </summary>
public class UploadMesHandler : INodeHandler
{
    public string NodeType => "UploadMes";
    public string DisplayName => "上传MES";
    public string Category => "外部交互";
    public string Description => "向 MES 系统上传出入库信息（事务后执行，失败不影响主流程）";
    public string? ConfigSchema => null;

    private readonly IMesClient _mesClient;
    private readonly MesClientOptions _mesOptions;
    private readonly ILogger<UploadMesHandler> _logger;

    public UploadMesHandler(
        IMesClient mesClient,
        IOptions<MesClientOptions> mesOptions,
        ILogger<UploadMesHandler> logger)
    {
        _mesClient = mesClient ?? throw new ArgumentNullException(nameof(mesClient));
        _mesOptions = mesOptions?.Value ?? throw new ArgumentNullException(nameof(mesOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        // 未启用 MES → 跳过
        if (!_mesOptions.Enable)
            return NodeResult.Skip("MES 未启用");

        // 取消时不上传
        if (context.IsCancelled)
            return NodeResult.Skip("任务已取消，跳过 MES 上传");

        var isInbound = context.FlowCategory == Cst.入库 || context.FlowCategory == Cst.入库双叉;
        var unitload = context.Unitload;

        // 确定 MES 参数
        int opType;
        Location? mesLocation;
        DateTime mesTime;

        if (isInbound)
        {
            opType = 1;
            mesLocation = context.TargetLocation;
            mesTime = DateTime.Now;
        }
        else
        {
            opType = 2;
            mesLocation = context.StartLocation;
            // 出库使用原始入库时间（由 UpdateUnitloadHandler 保存到 context.Data）
            mesTime = context.Data.GetValueOrDefault("OriginalLocationTime") as DateTime? ?? DateTime.Now;
        }

        if (mesLocation == null)
        {
            _logger.LogWarning("[FlowNode:UploadMes] 目标库位为空，跳过 MES 上传: BusinessId={BusinessId}", context.BusinessId);
            return NodeResult.Skip("目标库位为空");
        }

        // 收集容器码
        var codes = new List<string>();
        if (!string.IsNullOrWhiteSpace(unitload?.ContainerCode))
            codes.Add(unitload.ContainerCode);

        var additional = context.Data.GetValueOrDefault("AdditionalUnitloads") as List<Unitload>;
        if (additional != null)
        {
            codes.AddRange(additional
                .Select(u => u.ContainerCode)
                .Where(c => !string.IsNullOrWhiteSpace(c)));
        }

        if (codes.Count == 0)
        {
            _logger.LogWarning("[FlowNode:UploadMes] 容器码为空，跳过 MES 上传: BusinessId={BusinessId}", context.BusinessId);
            return NodeResult.Skip("容器码为空");
        }

        try
        {
            var mesResult = await _mesClient.SaveUploadMesInfoAsync(codes.ToArray(), mesLocation, mesTime, opType);
            _logger.LogInformation("[FlowNode:UploadMes] MES 上传结果: BusinessId={BusinessId}, Status={Status}, Msg={Msg}",
                context.BusinessId, mesResult.status, mesResult.message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FlowNode:UploadMes] MES 上传失败: BusinessId={BusinessId}", context.BusinessId);
            return NodeResult.Skip("MES 上传异常");
        }

        return NodeResult.Ok();
    }
}
