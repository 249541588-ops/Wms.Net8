using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Application.Ports;
using Wms.Core.Engine;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 工序推进节点 — NextOperation → CurrentOperation
/// </summary>
public class AdvanceOperationHandler : INodeHandler
{
    public string NodeType => "AdvanceOperation";
    public string DisplayName => "工序推进";
    public string Category => "业务逻辑";
    public string Description => "将 Unitload 的 NextOperation 推进为 CurrentOperation，查询新的 NextOperation";
    public string? ConfigSchema => null;

    private readonly IUnitloadService _unitloadService;
    private readonly ILogger<AdvanceOperationHandler> _logger;

    public AdvanceOperationHandler(IUnitloadService unitloadService, ILogger<AdvanceOperationHandler> logger)
    {
        _unitloadService = unitloadService ?? throw new ArgumentNullException(nameof(unitloadService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var unitload = context.Unitload;
        if (unitload == null)
            return Task.FromResult(NodeResult.Skip("无托盘"));

        var isCompletion = context.Phase == Cst.PhaseCompletion;

        // 取消时不推进工序
        if (isCompletion && context.IsCancelled)
            return Task.FromResult(NodeResult.Skip("任务已取消，跳过工序推进"));

        if (isCompletion)
        {
            unitload.CurrentOperation = unitload.NextOperation;
            unitload.NextOperation = _unitloadService.GetNextOperation(unitload.CurrentOperation);

            // 额外 Unitload 也推进工序（标准入库多容器码场景）
            var additional = context.Data.GetValueOrDefault("AdditionalUnitloads") as List<Unitload>;
            if (additional != null)
            {
                foreach (var u in additional)
                {
                    u.CurrentOperation = u.NextOperation;
                    u.NextOperation = _unitloadService.GetNextOperation(u.CurrentOperation);
                }
            }

            _logger.LogInformation("[FlowNode:AdvanceOperation] 工序推进: ContainerCode={ContainerCode}, Current={Current}, Next={Next}",
                unitload.ContainerCode, unitload.CurrentOperation, unitload.NextOperation);
        }

        return Task.FromResult(NodeResult.Ok());
    }
}
