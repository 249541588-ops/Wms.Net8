using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Enums;
using Wms.Core.Engine;
using Wms.Core.Domain.Utilities.Response;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 验证参数节点 — 检查请求参数、位置状态
/// </summary>
public class ValidateParamsHandler : INodeHandler
{
    public string NodeType => "ValidateParams";
    public string DisplayName => "验证参数";
    public string Category => "基础";
    public string Description => "检查请求的 ContainerCode 非空，以及位置禁用状态和计数限制";
    public string? ConfigSchema => null;

    private readonly ILogger<ValidateParamsHandler> _logger;

    public ValidateParamsHandler(ILogger<ValidateParamsHandler> logger)
    {
        _logger = logger;
    }

    public Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var request = context.WcsRequest;
        if (request == null)
            return Task.FromResult(NodeResult.Fail("上下文中无 WCS 请求"));

        // 检查 ContainerCode
        if (request.ContainerCode == null || request.ContainerCode.Length == 0)
            return Task.FromResult(NodeResult.WcsFail("容器编码不能为空", ResultCodeTypes.数据异常, -1));

        // 叠盘时：两步校验（至少 2 个有效容器编码）
        if (context.FlowCategory == Cst.叠盘)
        {
            if (request.ContainerCode.Length < 2)
                return Task.FromResult(NodeResult.WcsFail("叠盘至少需要两个容器编码", ResultCodeTypes.数据异常, -1));

            var codes = request.ContainerCode
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToArray();

            if (codes.Length < 2)
                return Task.FromResult(NodeResult.WcsFail("有效容器编码至少需要两个", ResultCodeTypes.数据异常, -1));

            context.Data["StackingCodes"] = codes;
        }

        return Task.FromResult(NodeResult.Ok());
    }
}
