using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Handlers.WcsRequest;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 创建运输任务节点 — 生成 TaskCode 并创建 TransTask
/// </summary>
public class CreateTransTaskHandler : INodeHandler
{
    public string NodeType => "CreateTransTask";
    public string DisplayName => "创建运输任务";
    public string Category => "业务逻辑";
    public string Description => "生成唯一 TaskCode，创建 TransTask 记录";
    public string? ConfigSchema => null;

    private readonly ILogger<CreateTransTaskHandler> _logger;

    public CreateTransTaskHandler(ILogger<CreateTransTaskHandler> logger)
    {
        _logger = logger;
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var unitload = context.Unitload;
        var startLocation = context.StartLocation;
        var targetLocation = context.TargetLocation;
        var request = context.WcsRequest;

        if (unitload == null || startLocation == null)
            return NodeResult.Fail("托盘或起始位置为空");

        // 出库时起点终点相同（目标位置由 WCS 控制出库口）
        var endLocation = targetLocation ?? startLocation;

        // 入库：存储所有容器码；其他（出库/移库/入库双叉）：只存储当前容器码
        var requestType = context.FlowCategory ?? Cst.入库;
        var ext1 = (requestType == Cst.入库)
            ? string.Join(";", request?.ContainerCode ?? [])
            : (context.CurrentContainerCode ?? string.Empty);

        var transTask = new Domain.Entities.Transport.TransTask
        {
            TaskCode = await TaskCodeGenerator.GenerateAsync(context.Db),
            TaskType = requestType,
            UnitloadId = unitload.UnitloadId,
            UnitloadCode = unitload.ContainerCode,
            StartLocationId = startLocation.LocationId,
            EndLocationId = endLocation.LocationId,
            ForWcs = true,
            WasSentToWcs = false,
            WareHouse = startLocation.AreaName,
            Ext1 = ext1,
            Ext2 = (requestType == Cst.入库)
                && context.Data.GetValueOrDefault("ValidatedUnitloads") is Dictionary<string, Unitload> validated
                ? string.Join(";", validated.Values
                    .Where(u => u.UnitloadId != unitload.UnitloadId)
                    .Select(u => u.UnitloadId))
                : string.Empty,
            LocationGroup = context.Data.GetValueOrDefault("SharedLocationGroup") as string
                ?? string.Empty
        };

        context.Db.TransTasks.Add(transTask);

        // 设置导航属性（确保 DatabaseWcsTaskBridge 回退时能获取正确的信息）
        transTask.Unitload = unitload;
        transTask.StartLocation = startLocation;
        transTask.EndLocation = endLocation;

        context.TransTask = transTask;

        return NodeResult.Ok();
    }
}
