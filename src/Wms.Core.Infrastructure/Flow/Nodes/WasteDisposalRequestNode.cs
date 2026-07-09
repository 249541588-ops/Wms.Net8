using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Engine;
using WcsRequestDto = Wms.Core.Application.DTOs.WcsRequest;

namespace Wms.Core.Engine.Nodes;

/// <summary>
/// 排废验证节点 — 验证容器 + 可选调用 MES/杭可 + 组装排废数据
/// 流程：验证容器 → 查 WasteBatchSetting → MES/杭可可选更新 → 组装排废字符串 → 返回 NodeResult
/// </summary>
public class WasteDisposalRequestNode : INodeHandler
{
    public string NodeType => "WasteDisposalRequest";
    public string DisplayName => "排废验证";
    public string Category => "业务逻辑";
    public string Description => "排废工位请求处理 — 验证容器 + 可选调用 MES/杭可 + 组装排废数据";
    public string? ConfigSchema => null;

    private readonly IMesClient _mesClient;
    private readonly IHangKeClient _hangkeClient;
    private readonly MesClientOptions _mesOptions;
    private readonly HangKeClientOptions _hangkeOptions;
    private readonly ILogger<WasteDisposalRequestNode> _logger;

    /// <summary>
    /// DCIR 排废工位 LocationCode（占位值，后续替换为实际值）
    /// </summary>
    private static readonly string[] DcirLocations = ["5101", "5102"];

    /// <summary>
    /// OCV3 排废工位 LocationCode（占位值，后续替换为实际值）
    /// </summary>
    private static readonly string[] Ocv3Locations = ["5111", "5112"];

    /// <summary>
    /// 化成排废工位 LocationCode（占位值，后续替换为实际值）
    /// </summary>
    private static readonly string[] HcLocations = ["1008", "1009"];

    /// <summary>
    /// DCIR 基础数据编码
    /// </summary>
    private const string DicDcir = "WASTEDISCHARGECHANNEL_DCR";

    /// <summary>
    /// OCV3 基础数据编码
    /// </summary>
    private const string DicOcv3 = "WASTEDISCHARGECHANNEL_OCV";

    /// <summary>
    /// 化成基础数据编码
    /// </summary>
    private const string DicHc = "WASTEDISCHARGECHANNEL_HC";

    public WasteDisposalRequestNode(
        IMesClient mesClient,
        IHangKeClient hangkeClient,
        IOptions<MesClientOptions> mesOptions,
        IOptions<HangKeClientOptions> hangkeOptions,
        ILogger<WasteDisposalRequestNode> logger)
    {
        _mesClient = mesClient ?? throw new ArgumentNullException(nameof(mesClient));
        _hangkeClient = hangkeClient ?? throw new ArgumentNullException(nameof(hangkeClient));
        _mesOptions = mesOptions?.Value ?? throw new ArgumentNullException(nameof(mesOptions));
        _hangkeOptions = hangkeOptions?.Value ?? throw new ArgumentNullException(nameof(hangkeOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson)
    {
        var location = context.StartLocation;
        var request = context.WcsRequest;

        if (location == null)
            return NodeResult.WcsFail("位置信息为空", ResultCodeTypes.数据异常, -1);

        _logger.LogInformation("[FlowNode:WasteDisposalRequest] 排废验证: 位置={Location}, 容器={Container}",
            location.LocationCode, string.Join(",", request?.ContainerCode ?? []));

        // 基础参数验证
        if (request?.ContainerCode == null || request.ContainerCode.Length == 0)
            return NodeResult.WcsFail("容器编码不能为空", ResultCodeTypes.数据异常, -1);

        var results = new List<object>();

        // 遍历容器码，逐个处理
        foreach (var containerCode in request.ContainerCode)
        {
            if (string.IsNullOrWhiteSpace(containerCode)) continue;

            // 3a. 查询 Unitload（含导航属性）
            var unitload = await context.Db.Unitloads
                .Include(u => u.UnitloadItems)!
                    .ThenInclude(ui => ui!.UnitloadItemDetails)
                .FirstOrDefaultAsync(u => u.ContainerCode == containerCode);
            if (unitload == null)
                return NodeResult.WcsFail($"托盘 {containerCode} 不存在", ResultCodeTypes.数据异常, -1);

            // 3b. 查 WasteBatchSetting 批次验证
            var setting = await context.Db.Set<WasteBatchSetting>()
                .FirstOrDefaultAsync(w => w.LocationCode == location.LocationCode
                    && (w.ContainerCode == null || w.ContainerCode == containerCode));

            if (setting != null)
            {
                var unitloadBatch = unitload.UnitloadItems?.FirstOrDefault()?.Batch;
                if (!string.Equals(unitloadBatch, setting.Batch, StringComparison.OrdinalIgnoreCase))
                {
                    return NodeResult.WcsFail(
                        $"托盘 {containerCode} 批次不匹配（当前: {unitloadBatch ?? "空"}, 要求: {setting.Batch}）",
                        ResultCodeTypes.排废批次不同, -1);
                }
            }

            // 3c. MES 集成（可选，容错）
            if (_mesOptions.Enable)
            {
                try
                {
                    await _mesClient.GetWasteDischargeInfoAsync(unitload);
                    _logger.LogInformation("[FlowNode:WasteDisposalRequest] MES 排废信息更新完成: Container={Container}", containerCode);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[FlowNode:WasteDisposalRequest] MES 排废信息查询失败（不阻断流程）: Container={Container}", containerCode);
                }
            }

            // 3d. 杭可集成（可选，容错，优先级高于 MES）
            if (_hangkeOptions.Enable)
            {
                try
                {
                    await _hangkeClient.GetDischargeInfoAsync(unitload);
                    _logger.LogInformation("[FlowNode:WasteDisposalRequest] 杭可排废信息更新完成: Container={Container}", containerCode);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[FlowNode:WasteDisposalRequest] 杭可排废信息查询失败（不阻断流程）: Container={Container}", containerCode);
                }
            }

            // 3e. 重新查询最新电芯状态（MES/杭可使用各自的 DbContext，需刷新）
            unitload = await context.Db.Unitloads
                .Include(u => u.UnitloadItems)!
                    .ThenInclude(ui => ui!.UnitloadItemDetails)
                .FirstOrDefaultAsync(u => u.ContainerCode == containerCode)
                ?? unitload;

            // 3f. 判断工位类型 + 查询基础数据（父记录 No → 子项 ParentId）
            var dicNo = GetDischargeDictionaryNo(location.LocationCode);
            var nameToValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (dicNo != null)
            {
                var parent = await context.Db.Set<BasicDictionary>()
                    .FirstOrDefaultAsync(x => x.No == dicNo);
                if (parent != null)
                {
                    var dictItems = await context.Db.Set<BasicDictionary>()
                        .Where(x => x.ParentId == parent.Id)
                        .ToListAsync();
                    foreach (var item in dictItems)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Name))
                            nameToValue[item.Name.Trim()] = item.Value?.Trim() ?? "A";
                    }
                }
            }

            // 3g. 获取电芯详情并排序
            var details = unitload.UnitloadItems?.FirstOrDefault()?.UnitloadItemDetails
                ?.OrderBy(d => d.LocIndex)
                .ToList();

            // 3h. 组装排废字符串
            var wasteBuilder = new System.Text.StringBuilder();
            int ngCount = 0;
            int totalCount = 0;

            if (details != null && details.Count > 0)
            {
                foreach (var detail in details)
                {
                    totalCount++;
                    char ch = ResolveWasteChar(detail, nameToValue);
                    if (ch != 'A') ngCount++;
                    wasteBuilder.Append(ch);
                }
            }

            var wasteString = wasteBuilder.ToString();

            // 3i. 组装返回数据
            var data = new
            {
                containerCode,
                locationCode = location.LocationCode,
                locationType = setting?.LocationType,
                currentBatch = unitload.UnitloadItems?.FirstOrDefault()?.Batch ?? string.Empty,
                requiredBatch = setting?.Batch ?? string.Empty,
                isBuiltIn = setting?.IsBuiltIn,
                wasteString,
                ngCount,
                totalCount
            };

            results.Add(data);

            _logger.LogInformation(
                "[FlowNode:WasteDisposalRequest] 排废验证完成: Container={Container}, WasteString={Waste}, NG={NgCount}/{Total}",
                containerCode, wasteString, ngCount, totalCount);
        }

        return NodeResult.Ok(new Dictionary<string, object?>
        {
            ["WasteDisposalResults"] = results
        });
    }

    /// <summary>
    /// 根据 LocationCode 判断排废工位类型，返回对应基础数据编码
    /// </summary>
    private static string? GetDischargeDictionaryNo(string? locationCode)
    {
        if (string.IsNullOrEmpty(locationCode)) return null;

        if (DcirLocations.Contains(locationCode))
            return DicDcir;

        if (Ocv3Locations.Contains(locationCode))
            return DicOcv3;

        if (HcLocations.Contains(locationCode))
            return DicHc;

        return null;
    }

    /// <summary>
    /// 根据电芯状态和基础数据匹对结果，返回排废字符
    /// A=正常, Y=排废, Z=无电芯
    /// </summary>
    private static char ResolveWasteChar(UnitloadItemDetail detail, Dictionary<string, string> nameToValue)
    {
        // Z: 无电芯（条码为空或等于空电芯码常量）
        if (string.IsNullOrWhiteSpace(detail.BarCode)
            || detail.BarCode == CommonTypes.电芯码空)
        {
            return 'Z';
        }

        if (detail.Status == Unitload_Enum.UnitloadItemDetailStatus.假电芯.ToString())
        {
            return 'X';
        }

        if (detail.Status == Unitload_Enum.UnitloadItemDetailStatus.正常.ToString())
        {
            return 'A';
        }

        // 基础数据匹对：xLevel → Name 匹配 → 取 Value 作为排废字符
        var xLevel = detail.xLevel?.Trim();
        if (!string.IsNullOrEmpty(xLevel) && nameToValue.Count > 0)
        {
            if (nameToValue.TryGetValue(xLevel, out var wasteChar) && !string.IsNullOrEmpty(wasteChar))
                return wasteChar[0]; // Value 直接作为排废字符（如 'A' 或 'Y'）
            return 'Y'; // 未匹配到 Name，默认排废
        }

        // 默认正常
        return 'A';
    }
}
