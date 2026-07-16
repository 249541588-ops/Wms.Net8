using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Application.Ports;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 工序推进节点 — NextOperation → CurrentOperation（双模式：工艺路线 / 硬编码）
/// </summary>
public class AdvanceOperationHandler : INodeHandler
{
    public string NodeType => "AdvanceOperation";
    public string DisplayName => "工序推进";
    public string Category => "业务逻辑";
    public string Description => "将 Unitload 的 NextOperation 推进为 CurrentOperation，查询新的 NextOperation（双模式兼容）";
    public string? ConfigSchema => null;

    private readonly IUnitloadService _unitloadService;
    private readonly IProcessRouteService _processRouteService;
    private readonly ILogger<AdvanceOperationHandler> _logger;

    public AdvanceOperationHandler(
        IUnitloadService unitloadService,
        IProcessRouteService processRouteService,
        ILogger<AdvanceOperationHandler> logger)
    {
        _unitloadService = unitloadService ?? throw new ArgumentNullException(nameof(unitloadService));
        _processRouteService = processRouteService ?? throw new ArgumentNullException(nameof(processRouteService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var unitload = context.Unitload;
        if (unitload == null)
            return NodeResult.Skip("无托盘");

        var isCompletion = context.Phase == Cst.PhaseCompletion;

        // 取消时不推进工序
        if (isCompletion && context.IsCancelled)
            return NodeResult.Skip("任务已取消，跳过工序推进");

        if (isCompletion)
        {
            await AdvanceUnitloadOperation(unitload, context.CurrentUser);

            // 额外 Unitload 也推进工序（标准入库多容器码场景）
            var additional = context.Data.GetValueOrDefault("AdditionalUnitloads") as List<Unitload>;
            if (additional != null)
            {
                foreach (var u in additional)
                {
                    await AdvanceUnitloadOperation(u, context.CurrentUser);
                }
            }

            _logger.LogInformation("[FlowNode:AdvanceOperation] 工序推进: ContainerCode={ContainerCode}, Current={Current}, Next={Next}, Mode={Mode}",
                unitload.ContainerCode, unitload.CurrentOperation, unitload.NextOperation,
                unitload.ProcessRouteVersionId.HasValue ? "Route" : "Hardcoded");
        }

        return NodeResult.Ok();
    }

    /// <summary>
    /// 双模式推进单个托盘的工序
    /// </summary>
    private async Task AdvanceUnitloadOperation(Unitload unitload, string? operatorName)
    {
        if (unitload.ProcessRouteVersionId.HasValue)
        {
            // 工艺路线模式
            var handled = await _processRouteService.AdvanceOperationAsync(unitload, operatorName);
            if (!handled)
            {
                // 路线数据异常，回退硬编码
                unitload.CurrentOperation = unitload.NextOperation;
                unitload.NextOperation = _unitloadService.GetNextOperation(unitload.CurrentOperation);
            }
        }
        else
        {
            // 硬编码模式（现有逻辑不变）
            unitload.CurrentOperation = unitload.NextOperation;
            unitload.NextOperation = _unitloadService.GetNextOperation(unitload.CurrentOperation);
        }
    }
}
