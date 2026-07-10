using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using Wms.Core.Application.DTOs.Mes;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Clients;

/// <summary>
/// MES 通信客户端默认实现
/// </summary>
public class DefaultMesClient : IMesClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DefaultMesClient> _logger;
    private readonly MesClientOptions _options;
    private readonly WmsDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="logger"></param>
    /// <param name="options"></param>
    /// <param name="db"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public DefaultMesClient(
        HttpClient httpClient,
        ILogger<DefaultMesClient> logger,
        IOptions<MesClientOptions> options,
        WmsDbContext db)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 根据巷道获取 Mes 对应参数
    /// </summary>
    /// <param name="_loc"></param>
    /// <returns></returns>
    private MesInterfaceVM? GetInterfaceVM(Location _loc)
    {
        if (_loc == null)
            return null;

        MesInterfaceVM _vm = new MesInterfaceVM();
        _vm.tenantID = 1000068;

        if (_loc.LocationType == Location_Enum.LocationType.R.ToString())
        {
            var lanewayCode = _loc.Rack?.Laneway?.LanewayCode;
            switch (lanewayCode)
            {
                case "L1":
                    _vm.technicsProcessCode = "C1901"; _vm.technicsProcessName = "高温静置";
                    _vm.deviceCode = "JC-SH-GWJZK-01"; _vm.deviceName = "高温静置库1号堆垛机";
                    break;
                case "L2":
                    _vm.technicsProcessCode = "C1901"; _vm.technicsProcessName = "高温静置";
                    _vm.deviceCode = "JC-SH-GWJZK-02"; _vm.deviceName = "高温静置库2号堆垛机";
                    break;
                case "L3":
                    _vm.technicsProcessCode = "C1901"; _vm.technicsProcessName = "高温静置";
                    _vm.deviceCode = "JC-SH-GWJZK-03"; _vm.deviceName = "高温静置库3号堆垛机";
                    break;
                case "L4":
                    _vm.technicsProcessCode = "C1901"; _vm.technicsProcessName = "高温静置";
                    _vm.deviceCode = "JC-SH-GWJZK-04"; _vm.deviceName = "高温静置库4号堆垛机";
                    break;
                case "L5":
                    _vm.technicsProcessCode = "C2001"; _vm.technicsProcessName = "化成立体库";
                    _vm.deviceCode = "JC-SH-HCK-01"; _vm.deviceName = "化成立体库1号堆垛机";
                    break;
                case "L6":
                    _vm.technicsProcessCode = "C2601"; _vm.technicsProcessName = "分容立体库";
                    _vm.deviceCode = "JC-SH-FRK-01"; _vm.deviceName = "分容立体库1号堆垛机";
                    break;
                case "L7":
                    _vm.technicsProcessCode = "C2601"; _vm.technicsProcessName = "分容立体库";
                    _vm.deviceCode = "JC-SH-FRK-02"; _vm.deviceName = "分容立体库2号堆垛机";
                    break;
                case "L9":
                    _vm.technicsProcessCode = "C2602"; _vm.technicsProcessName = "24H常温静置";
                    _vm.deviceCode = "JC-SH-JZK-01"; _vm.deviceName = "24H静置库1号堆垛机";
                    break;
                case "L8":
                    _vm.technicsProcessCode = "C2801"; _vm.technicsProcessName = "七天常温静置";
                    _vm.deviceCode = "JC-SH-JZK-02"; _vm.deviceName = "常温静置库1号堆垛机";
                    break;
                case "L10":
                    _vm.technicsProcessCode = "C2801"; _vm.technicsProcessName = "七天常温静置";
                    _vm.deviceCode = "JC-SH-JZK-03"; _vm.deviceName = "常温静置库2号堆垛机";
                    break;
                case "L11":
                    _vm.technicsProcessCode = "C3001"; _vm.technicsProcessName = "成品库";
                    _vm.deviceCode = "JC-SH-CPK-01"; _vm.deviceName = "成品库1号堆垛机";
                    break;
            }
        }
        else
        {
            switch (_loc.LocationCode)
            {
                case "5139":
                case "5133":
                    _vm.technicsProcessCode = "C3000"; _vm.technicsProcessName = "分档";
                    _vm.deviceCode = "JC-SH-FDJ-01"; _vm.deviceName = "分档1号机";
                    break;
                case "5120":
                case "5114":
                    _vm.technicsProcessCode = "C3000"; _vm.technicsProcessName = "分档";
                    _vm.deviceCode = "JC-SH-FDJ-02"; _vm.deviceName = "分档2号机";
                    break;
            }
        }
        return _vm;
    }

    /// <summary>
    /// 数据保存到 UploadMesInfo
    /// </summary>
    /// <param name="containerCodes"></param>
    /// <param name="_loc"></param>
    /// <param name="_currentTime"></param>
    /// <param name="opType"></param>
    /// <returns></returns>
    public async Task<MesResult> SaveUploadMesInfoAsync(string[] containerCodes, Location _loc, DateTime _currentTime, int opType)
    {
        if (containerCodes.Length == 0)
            return new MesResult { status = true, message = "无容器编码" };

        // 批量查询所有托盘（含明细、电芯详情、物料）
        var unitloads = await _db.Unitloads
            .Include(u => u.UnitloadItems)
                .ThenInclude(ui => ui!.UnitloadItemDetails)
            .Include(u => u.UnitloadItems)
                .ThenInclude(ui => ui!.Material)
            .Where(u => containerCodes.Contains(u.ContainerCode))
            .ToListAsync();

        List<UploadMesInfo> _list = new List<UploadMesInfo>();

        foreach (string containerCode in containerCodes)
        {
            var _u = unitloads.FirstOrDefault(u => u.ContainerCode == containerCode);
            if (_u == null)
                continue;
            else if (_u.UnitloadItems?.Any(y => y.Material?.MaterialCode == CommonTypes.空托盘) == true)
                continue;
            else
            {
                foreach (UnitloadItem _ui in _u.UnitloadItems ?? [])
                {
                    List<MesInterfaceVM> _listMes = new List<MesInterfaceVM>();

                    foreach (UnitloadItemDetail _detail in _ui.UnitloadItemDetails ?? [])
                    {
                        MesInterfaceVM? _vm = GetInterfaceVM(_loc);
                        if (_vm == null) continue;

                        _vm.productCode = string.Format("{0}", _detail.BarCode);
                        _vm.productCount = 1;
                        _vm.productQuality = 1;
                        _vm.userAccount = string.Empty;
                        _vm.startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        _vm.endTime = DateTime.Now.AddMinutes(3).ToString("yyyy-MM-dd HH:mm:ss");
                        _vm.produceDate = DateTime.Now.ToString("yyyy-MM-dd");

                        ProduceParamEntityList _entityList = new ProduceParamEntityList()
                        {
                            producode = _detail.BarCode,
                            technicsParamCode = "WAR_NUM",
                            technicsParamName = "库位号",
                            technicsParamValue = _loc.LocationCode,
                            technicsParamQuality = "1",
                        };
                        _vm.produceParamEntityList.Add(_entityList);

                        ProduceParamEntityList _entityList1 = new ProduceParamEntityList()
                        {
                            producode = _detail.BarCode,
                            technicsParamCode = "TRA_NUM",
                            technicsParamName = "托盘号",
                            technicsParamValue = _ui.BoxCode,
                            technicsParamQuality = "1",
                        };
                        _vm.produceParamEntityList.Add(_entityList1);

                        if (opType == 1)
                        {
                            ProduceParamEntityList _entityList2 = new ProduceParamEntityList()
                            {
                                producode = _detail.BarCode,
                                technicsParamCode = "IN_WAR_TIM",
                                technicsParamName = "入库时间",
                                technicsParamValue = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                technicsParamQuality = "1",
                            };
                            _vm.produceParamEntityList.Add(_entityList2);
                        }
                        else
                        {
                            ProduceParamEntityList _entityList2 = new ProduceParamEntityList()
                            {
                                producode = _detail.BarCode,
                                technicsParamCode = "IN_WAR_TIM",
                                technicsParamName = "入库时间",
                                technicsParamValue = _currentTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                technicsParamQuality = "1",
                            };
                            _vm.produceParamEntityList.Add(_entityList2);

                            ProduceParamEntityList _entityList3 = new ProduceParamEntityList()
                            {
                                producode = _detail.BarCode,
                                technicsParamCode = "OUT_WAR_TIM",
                                technicsParamName = "出库时间",
                                technicsParamValue = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                technicsParamQuality = "1",
                            };
                            _vm.produceParamEntityList.Add(_entityList3);

                            ProduceParamEntityList _entityList4 = new ProduceParamEntityList()
                            {
                                producode = _detail.BarCode,
                                technicsParamCode = "STE_TIM",
                                technicsParamName = "静置时间",
                                technicsParamValue = DateTime.Now.Subtract(_currentTime).TotalHours.ToString("0.#"),
                                technicsParamQuality = "1",
                            };
                            _vm.produceParamEntityList.Add(_entityList4);
                        }

                        _listMes.Add(_vm);
                    }

                    UploadMesInfo uploadMesInfo = new UploadMesInfo()
                    {
                        ContainerCode = _ui.BoxCode,
                        LocationCode = _loc.LocationCode,
                        BizType = opType == 1 ? "入库" : "出库",
                        Direction = opType == 1 ? 1 : -1,
                        OpType = opType == 1 ? "自动入库" : "自动出库",
                        CurrentOperation = _u.CurrentOperation,
                        MesIsFlag = 1,
                        MesMsg = string.Empty,
                        ctime = DateTime.Now,
                        MestextInfo = JsonSerializer.Serialize(_listMes, JsonOptions),
                        Quantity = _ui.Quantity,
                    };

                    _list.Add(uploadMesInfo);
                }
            }
        }

        foreach (var item in _list)
        {
            _db.UploadMesInfos.Add(item);
        }
        await _db.SaveChangesAsync();

        _logger.LogInformation("[MES客户端] 保存 UploadMesInfo 共 {Count} 条", _list.Count);

        return new MesResult { status = true, message = $"成功保存 {_list.Count} 条 MES 信息" };
    }

    /// <summary>
    /// 获取K值排废信息 — 调用 MES 接口校验电芯 K值，标记 NG 电芯
    /// </summary>
    /// <param name="unitload"></param>
    /// <returns></returns>
    public async Task<MesResult> GetWasteDischargeInfoAsync(Unitload unitload)
    {
        MesResult resultInfo = new MesResult()
        {
            status = false,
        };

        try
        {
            if (unitload == null)
                throw new Exception("托盘信息不能空");

            var _ui = unitload.UnitloadItems?.FirstOrDefault();
            if (_ui == null)
                throw new Exception($"托盘 {unitload.ContainerCode} 无明细项");

            var _uidetail = _ui.UnitloadItemDetails;

            _logger.LogInformation("[MES客户端] 托盘 {ContainerCode} 获取KValue数据，传递数据条数：{Count}",
                unitload.ContainerCode, _uidetail?.Count ?? 0);

            VerifyKValueVM verifyKValue = new VerifyKValueVM()
            {
                tpCode = unitload.ContainerCode,
                type = unitload.OperationNumber,
                productCodes = string.Join(",", _uidetail?.Select(y => y.BarCode) ?? [])
            };

            string model = JsonSerializer.Serialize(verifyKValue, JsonOptions);

            _logger.LogInformation("[MES客户端] 托盘 {ContainerCode} 获取KValue数据，传递数据：{Data}",
                unitload.ContainerCode, model);

            var url = $"{_options.Endpoint.TrimEnd('/')}/api/produce/private/kanalyse/check";
            var content = new StringContent(model, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            resultInfo = JsonSerializer.Deserialize<MesResult>(responseBody, JsonOptions)
                          ?? new MesResult { status = false, message = "MES 返回空响应" };

            _logger.LogInformation("[MES客户端] 托盘 {ContainerCode} 获取KValue数据，返回数据：{Data}",
                unitload.ContainerCode, responseBody);

            if (!resultInfo.status)
            {
                _logger.LogWarning("[MES客户端] 托盘 {ContainerCode} 获取KValue数据失败，反馈结果为：{Code},{Message}",
                    unitload.ContainerCode, resultInfo.code, resultInfo.message);
            }
            else
            {
                if (resultInfo.data != null && resultInfo.data.ToString().Length > 0)
                {
                    string[] _productCodes = resultInfo.data.ToString().Split(',');

                    foreach (UnitloadItemDetail detail2 in _uidetail ?? [])
                    {
                        if (_productCodes.Contains(detail2.BarCode))
                        {
                            if (detail2.Status.Equals(Unitload_Enum.UnitloadItemDetailStatus.正常.ToString()))
                            {
                                detail2.Status = Unitload_Enum.UnitloadItemDetailStatus.NG.ToString();
                                detail2.xLevel = "8";
                                detail2.Comment = "K值NG";
                            }
                        }
                        else
                            continue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MES客户端] 托盘 {ContainerCode} 获取KValue异常",
                unitload?.ContainerCode);
            resultInfo.errorMsg = "KValue接口系统错误:" + ex.Message;
        }

        return resultInfo;
    }

    /// <summary>
    /// 推送 UploadMesInfo 的 MestextInfo 到 MES 批量接口（队列消费端调用）
    /// </summary>
    /// <param name="mestextInfo"></param>
    /// <returns></returns>
    public async Task<MesResult> PushMesInfoAsync(string mestextInfo)
    {
        var url = $"{_options.Endpoint.TrimEnd('/')}/api/access/produce/open/batch/add";
        var content = new StringContent(mestextInfo, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[MES客户端] PushMesInfo HTTP={Status} 响应: {Body}", (int)response.StatusCode, responseBody);

        // 先判 HTTP 状态：非 2xx 时直接判定失败，避免 MES 返回 500 + 非 JSON 体时反序列化异常掩盖真实错误
        if (!response.IsSuccessStatusCode)
            return new MesResult { status = false, code = ((int)response.StatusCode).ToString(), message = responseBody };

        return JsonSerializer.Deserialize<MesResult>(responseBody, JsonOptions)
               ?? new MesResult { status = false, message = "MES 返回空响应" };
    }

    /// <summary>
    /// 自动分档保存 UploadMesInfo
    /// </summary>
    /// <param name="_ui"></param>
    /// <param name="_loc"></param>
    /// <returns></returns>
    public async Task<MesResult> SaveUploadMesInfoByAutomaticAsync(UnitloadItem _ui, Location _loc)
    {
        List<UploadMesInfo> _list = new List<UploadMesInfo>();
        List<MesInterfaceVM> _listMes = new List<MesInterfaceVM>();

        foreach (UnitloadItemDetail _detail in _ui.UnitloadItemDetails ?? [])
        {
            MesInterfaceVM? _vm = GetInterfaceVM(_loc);
            if (_vm == null) continue;

            _vm.productCode = string.Format("{0}", _detail.BarCode);
            _vm.productCount = 1;
            _vm.productQuality = 1;
            _vm.userAccount = string.Empty;
            _vm.startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _vm.endTime = DateTime.Now.AddMinutes(3).ToString("yyyy-MM-dd HH:mm:ss");
            _vm.produceDate = DateTime.Now.ToString("yyyy-MM-dd");

            ProduceParamEntityList _entityList = new ProduceParamEntityList()
            {
                producode = _detail.BarCode,
                technicsParamCode = "CELL_GEAR",
                technicsParamName = "电芯档位",
                technicsParamValue = _detail.xLevel,
                technicsParamQuality = "1",
            };
            _vm.produceParamEntityList.Add(_entityList);
            _listMes.Add(_vm);
        }

        UploadMesInfo uploadMesInfo = new UploadMesInfo()
        {
            ContainerCode = _ui.BoxCode,
            LocationCode = _loc.LocationCode,
            BizType = "分档",
            Direction = 0,
            OpType = "自动分档",
            CurrentOperation = _ui.Unitload?.CurrentOperation,
            MesIsFlag = 1,
            MesMsg = string.Empty,
            ctime = DateTime.Now,
            MestextInfo = JsonSerializer.Serialize(_listMes, JsonOptions),
        };

        _list.Add(uploadMesInfo);

        _db.UploadMesInfos.AddRange(_list);
        await _db.SaveChangesAsync();

        return new MesResult { status = true, message = "自动分档 MES 信息保存成功" };
    }

    /// <summary>
    /// 手工分档保存 UploadMesInfo
    /// </summary>
    /// <param name="batteryCode"></param>
    /// <param name="Level"></param>
    /// <param name="_loc"></param>
    /// <returns></returns>
    public async Task<MesResult> SaveUploadMesInfoManualAsync(string batteryCode, string Level, Location _loc)
    {
        List<UploadMesInfo> _list = new List<UploadMesInfo>();
        List<MesInterfaceVM> _listMes = new List<MesInterfaceVM>();

        {
            MesInterfaceVM? _vm = GetInterfaceVM(_loc);
            if (_vm != null)
            {
                _vm.productCode = string.Format("{0}", batteryCode);
                _vm.productCount = 1;
                _vm.productQuality = 1;
                _vm.userAccount = string.Empty;
                _vm.startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _vm.endTime = DateTime.Now.AddMinutes(3).ToString("yyyy-MM-dd HH:mm:ss");
                _vm.produceDate = DateTime.Now.ToString("yyyy-MM-dd");

                ProduceParamEntityList _entityList = new ProduceParamEntityList()
                {
                    producode = batteryCode,
                    technicsParamCode = "CELL_GEAR",
                    technicsParamName = "电芯档位",
                    technicsParamValue = Level,
                    technicsParamQuality = "1",
                };
                _vm.produceParamEntityList.Add(_entityList);
                _listMes.Add(_vm);
            }
        }

        UploadMesInfo uploadMesInfo = new UploadMesInfo()
        {
            ContainerCode = DateTime.Now.ToString("yyyyMMddHHmmss") + Random.Shared.Next(1000, 9999),
            LocationCode = _loc.LocationCode,
            BizType = "分档",
            Direction = 0,
            OpType = "自动分档",
            CurrentOperation = "分档",
            MesIsFlag = 1,
            MesMsg = string.Empty,
            ctime = DateTime.Now,
            MestextInfo = JsonSerializer.Serialize(_listMes, JsonOptions),
        };

        _list.Add(uploadMesInfo);

        _db.UploadMesInfos.AddRange(_list);
        await _db.SaveChangesAsync();

        return new MesResult { status = true, message = "手工分档 MES 信息保存成功" };
    }
}
