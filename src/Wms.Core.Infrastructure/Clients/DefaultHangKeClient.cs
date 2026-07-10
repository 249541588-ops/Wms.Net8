using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Wms.Core.Application.DTOs.HangKe;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Utilities.Response;
using Wms.Core.Infrastructure.Security;

namespace Wms.Core.Infrastructure.Clients;

/// <summary>
/// 杭可设备通信客户端默认实现
/// </summary>
public class DefaultHangKeClient : IHangKeClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DefaultHangKeClient> _logger;
    private readonly HangKeClientOptions _options;

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
    /// <exception cref="ArgumentNullException"></exception>
    public DefaultHangKeClient(
        HttpClient httpClient,
        ILogger<DefaultHangKeClient> logger,
        IOptions<HangKeClientOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 注销托盘 — 通过 SOAP XML 调用杭可接口
    /// </summary>
    /// <param name="TrayCode"></param>
    /// <returns></returns>
    public async Task<ResultInfo> CancelTrayAsync(string TrayCode)
    {
        ResultInfo resultInfo = new ResultInfo();

        try
        {
            if (string.IsNullOrWhiteSpace(TrayCode))
                throw new Exception("托盘条码不能空");

            int _DataType = 0;
            if (TrayCode.StartsWith(CommonTypes.托盘码前缀_化成))
                _DataType = (int)DataType_Enum.化成;
            else if (TrayCode.StartsWith(CommonTypes.托盘码前缀_分容))
                _DataType = (int)DataType_Enum.分容;

            if (_DataType == 0)
                throw new Exception("数据类型错误");

            var payload = new { DataType = _DataType, TrayCode = TrayCode };
            string model = JsonSerializer.Serialize(payload, JsonOptions);

            _logger.LogInformation("[杭可客户端] 托盘 {TrayCode} 注销，传递数据：{Data}", TrayCode, model);

            // 构造 SOAP XML 信封
            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Header>
    <PackSoapHeader xmlns=""http://tempuri.org/"">
      <UserName>{_options.UserName}</UserName>
      <PassWord>{_options.PassWord}</PassWord>
    </PackSoapHeader>
  </soap:Header>
  <soap:Body>
    <CheckOutTray xmlns=""http://tempuri.org/"">
      <json>{System.Security.SecurityElement.Escape(model)}</json>
    </CheckOutTray>
  </soap:Body>
</soap:Envelope>";

            var url = $"{_options.Endpoint.TrimEnd('/')}/CheckOutTray";
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "\"http://tempuri.org/CheckOutTray\"");

            using var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[杭可客户端] 托盘 {TrayCode} 注销，返回数据：{Data}", TrayCode, responseBody);

            // 解析 SOAP 响应，提取 CheckOutTrayResult
            var doc = XmlSafety.ParseSafe(responseBody);
            var ns = "http://tempuri.org/";
            var resultEl = doc.Descendants(XName.Get("CheckOutTrayResult", ns)).FirstOrDefault();
            if (resultEl != null)
            {
                resultInfo = JsonSerializer.Deserialize<ResultInfo>(resultEl.Value, JsonOptions)
                            ?? new ResultInfo { ResultCode = -1, ResultMessage = "响应解析失败" };
            }
            else
            {
                resultInfo.ResultCode = -1;
                resultInfo.ResultMessage = "SOAP 响应中未找到 CheckOutTrayResult";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[杭可客户端] 托盘 {TrayCode} 注销异常", TrayCode);
            resultInfo.ResultMessage = "杭可托盘注销接口系统错误:" + ex.Message;
        }

        return resultInfo;
    }

    /// <summary>
    /// 化成组盘 — 通过 SOAP XML 调用杭可接口
    /// </summary>
    /// <param name="unitload"></param>
    /// <returns></returns>
    public async Task<ResultInfo> ChemicalPalletizeAsync(Unitload unitload)
    {
        ResultInfo resultInfo = new ResultInfo();

        try
        {
            if (unitload == null)
                return new ResultInfo { ResultCode = 0, ResultMessage = "参数为空" };

            HKModel kModel = new HKModel();
            kModel.LoadNum = unitload.OperationNumber;
            kModel.TrayCode = unitload.ContainerCode;

            List<HKModelList> kModelLists = new List<HKModelList>();
            var details = unitload.UnitloadItems?.FirstOrDefault()?.UnitloadItemDetails;

            if (details != null)
            {
                foreach (var item in details)
                {
                    if (item.Status == "假电芯")
                        continue;

                    kModelLists.Add(new HKModelList
                    {
                        CellSn = item.BarCode,
                        Channel = item.LocIndex
                    });
                }
            }
            kModel.CellList = kModelLists;

            string model = JsonSerializer.Serialize(kModel, JsonOptions);
            _logger.LogInformation("[杭可客户端] 托盘 {TrayCode} 化成组盘，传递数据：{Data}", unitload.ContainerCode, model);

            // 构造 SOAP XML 信封
            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Header>
    <PackSoapHeader xmlns=""http://tempuri.org/"">
      <UserName>{_options.UserName}</UserName>
      <PassWord>{_options.PassWord}</PassWord>
    </PackSoapHeader>
  </soap:Header>
  <soap:Body>
    <LoadTrayDataHC xmlns=""http://tempuri.org/"">
      <json>{System.Security.SecurityElement.Escape(model)}</json>
    </LoadTrayDataHC>
  </soap:Body>
</soap:Envelope>";

            var url = $"{_options.Endpoint.TrimEnd('/')}/LoadTrayDataHC";
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "\"http://tempuri.org/LoadTrayDataHC\"");

            using var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[杭可客户端] 托盘 {TrayCode} 化成组盘，返回数据：{Data}", unitload.ContainerCode, responseBody);

            // 解析 SOAP 响应
            var doc = XmlSafety.ParseSafe(responseBody);
            var ns = "http://tempuri.org/";
            var resultEl = doc.Descendants(XName.Get("LoadTrayDataHCResult", ns)).FirstOrDefault();
            if (resultEl != null)
            {
                resultInfo = JsonSerializer.Deserialize<ResultInfo>(resultEl.Value, JsonOptions)
                            ?? new ResultInfo { ResultCode = -1, ResultMessage = "响应解析失败" };
            }
            else
            {
                resultInfo.ResultCode = -1;
                resultInfo.ResultMessage = "SOAP 响应中未找到 LoadTrayDataHCResult";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[杭可客户端] 托盘 {TrayCode} 化成组盘异常", unitload?.ContainerCode);
            resultInfo.ResultMessage = "杭可接口系统错误:" + ex.Message;
        }

        return resultInfo;
    }

    /// <summary>
    /// 分容组盘 — 通过 SOAP XML 调用杭可接口
    /// </summary>
    /// <param name="unitload"></param>
    /// <returns></returns>
    public async Task<ResultInfo> SeparatePalletizeAsync(Unitload unitload)
    {
        ResultInfo resultInfo = new ResultInfo();

        try
        {
            if (unitload == null)
                return new ResultInfo { ResultCode = 0, ResultMessage = "参数为空" };

            HKModel kModel = new HKModel();
            kModel.LoadNum = unitload.OperationNumber;
            kModel.TrayCode = unitload.ContainerCode;

            if (unitload.OperationNumber == Convert.ToInt32(Unitload_Enum.OperationNumber.二次))
                kModel.IsCheck = 0;
            else
            {
                // 预抽检=2 → 不抽检(0)，随机抽检=4 → 抽检(1)
                if (unitload.IsAdvance == Convert.ToInt32(Unitload_Enum.UnitloadAdvance.预测))
                    kModel.IsCheck = 0;
                else if (unitload.IsAdvance == Convert.ToInt32(Unitload_Enum.UnitloadAdvance.抽检))
                    kModel.IsCheck = 1;
                else
                    kModel.IsCheck = 0;
            }

            var details = unitload.UnitloadItems?.FirstOrDefault()?.UnitloadItemDetails;
            List<HKModelList> lists = new List<HKModelList>();

            if (details != null)
            {
                foreach (var item in details)
                {
                    lists.Add(new HKModelList
                    {
                        CellSn = item.BarCode,
                        Channel = item.LocIndex
                    });
                }
            }
            kModel.CellList = lists;

            string model = JsonSerializer.Serialize(kModel, JsonOptions);
            _logger.LogInformation("[杭可客户端] 托盘 {TrayCode} 分容组盘，传递数据：{Data}", unitload.ContainerCode, model);

            // 构造 SOAP XML 信封
            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Header>
    <PackSoapHeader xmlns=""http://tempuri.org/"">
      <UserName>{_options.UserName}</UserName>
      <PassWord>{_options.PassWord}</PassWord>
    </PackSoapHeader>
  </soap:Header>
  <soap:Body>
    <LoadTrayDataFR xmlns=""http://tempuri.org/"">
      <json>{System.Security.SecurityElement.Escape(model)}</json>
    </LoadTrayDataFR>
  </soap:Body>
</soap:Envelope>";

            var url = $"{_options.Endpoint.TrimEnd('/')}/LoadTrayDataFR";
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "\"http://tempuri.org/LoadTrayDataFR\"");

            using var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[杭可客户端] 托盘 {TrayCode} 分容组盘，返回数据：{Data}", unitload.ContainerCode, responseBody);

            // 解析 SOAP 响应
            var doc = XmlSafety.ParseSafe(responseBody);
            var ns = "http://tempuri.org/";
            var resultEl = doc.Descendants(XName.Get("LoadTrayDataFRResult", ns)).FirstOrDefault();
            if (resultEl != null)
            {
                resultInfo = JsonSerializer.Deserialize<ResultInfo>(resultEl.Value, JsonOptions)
                            ?? new ResultInfo { ResultCode = -1, ResultMessage = "响应解析失败" };
            }
            else
            {
                resultInfo.ResultCode = -1;
                resultInfo.ResultMessage = "SOAP 响应中未找到 LoadTrayDataFRResult";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[杭可客户端] 托盘 {TrayCode} 分容组盘异常", unitload?.ContainerCode);
            resultInfo.ResultMessage = "杭可接口系统错误:" + ex.Message;
        }

        return resultInfo;
    }

    /// <summary>
    /// 获取排废信息 — 通过 SOAP XML 调用杭可接口
    /// </summary>
    /// <param name="unitload"></param>
    /// <returns></returns>
    public async Task<ResultInfo> GetDischargeInfoAsync(Unitload unitload)
    {
        ResultInfo resultInfo = new ResultInfo();

        try
        {
            if (unitload == null)
                throw new Exception("托盘信息不能空");

            int _DataType = 0;
            if (unitload.ContainerCode.StartsWith(CommonTypes.托盘码前缀_化成))
                _DataType = (int)DataType_Enum.化成;
            else if (unitload.ContainerCode.StartsWith(CommonTypes.托盘码前缀_分容))
                _DataType = (int)DataType_Enum.分容;

            if (_DataType == 0)
                throw new Exception("数据类型错误");

            var payload = new { DataType = _DataType, TrayCode = unitload.ContainerCode };
            string model = JsonSerializer.Serialize(payload, JsonOptions);

            _logger.LogInformation("[杭可客户端] 托盘 {TrayCode} 获取排废数据，传递数据：{Data}", unitload.ContainerCode, model);

            // 构造 SOAP XML 信封
            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Header>
    <PackSoapHeader xmlns=""http://tempuri.org/"">
      <UserName>{_options.UserName}</UserName>
      <PassWord>{_options.PassWord}</PassWord>
    </PackSoapHeader>
  </soap:Header>
  <soap:Body>
    <GetTrayDataByTrayCode xmlns=""http://tempuri.org/"">
      <json>{System.Security.SecurityElement.Escape(model)}</json>
    </GetTrayDataByTrayCode>
  </soap:Body>
</soap:Envelope>";

            var url = $"{_options.Endpoint.TrimEnd('/')}/GetTrayDataByTrayCode";
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "\"http://tempuri.org/GetTrayDataByTrayCode\"");

            using var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[杭可客户端] 托盘 {TrayCode} 获取排废数据，返回数据：{Data}", unitload.ContainerCode, responseBody);

            // 解析 SOAP 响应
            var doc = XmlSafety.ParseSafe(responseBody);
            var ns = "http://tempuri.org/";
            var resultEl = doc.Descendants(XName.Get("GetTrayDataByTrayCodeResult", ns)).FirstOrDefault();
            if (resultEl != null)
            {
                resultInfo = JsonSerializer.Deserialize<ResultInfo>(resultEl.Value, JsonOptions)
                            ?? new ResultInfo { ResultCode = -1, ResultMessage = "响应解析失败" };
            }
            else
            {
                resultInfo.ResultCode = -1;
                resultInfo.ResultMessage = "SOAP 响应中未找到 GetTrayDataByTrayCodeResult";
            }

            if (resultInfo.ResultData == null)
            {
                resultInfo.ResultMessage = "电芯明细不能为空，请联系杭可！";
            }
            else
            {
                var cells = JsonSerializer.Deserialize<CellCallback>(resultInfo.ResultData.ToString()!, JsonOptions);

                var details = unitload.UnitloadItems?.FirstOrDefault()?.UnitloadItemDetails;
                if (details != null && cells?.JXSData != null)
                {
                    foreach (var detail in details)
                    {
                        foreach (var cell in cells.JXSData)
                        {
                            if (cell.CellSn == detail.BarCode && !string.IsNullOrWhiteSpace(cell.CellSn))
                            {
                                string[] _pick = (cell.Pick ?? "").Split(',');
                                if (cell.Status == 0)
                                {
                                    detail.Status = Unitload_Enum.UnitloadItemDetailStatus.正常.ToString();
                                }
                                else
                                {
                                    detail.Status = Unitload_Enum.UnitloadItemDetailStatus.NG.ToString();
                                    detail.xLevel = _pick[0];
                                    detail.Comment = _pick.Length > 1 ? _pick[1] : string.Empty;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[杭可客户端] 托盘 {TrayCode} 获取排废异常", unitload?.ContainerCode);
            resultInfo.ResultMessage = "杭可接口系统错误:" + ex.Message;
        }

        return resultInfo;
    }

    /// <summary>
    /// 出入口通知 — 通过 SOAP XML 调用杭可接口
    /// </summary>
    /// <param name="Position"></param>
    /// <param name="TrayCode"></param>
    /// <param name="InOutType"></param>
    /// <returns></returns>
    public async Task<ResultInfo> InOutNotifyAsync(string Position, string TrayCode, InOutType_Enum InOutType)
    {
        ResultInfo resultInfo = new ResultInfo();

        try
        {
            if (string.IsNullOrWhiteSpace(Position))
                throw new Exception("库位编码不能空");

            if (string.IsNullOrWhiteSpace(TrayCode))
                throw new Exception("托盘条码不能空");

            int _DataType = 0;
            if (TrayCode.StartsWith(CommonTypes.托盘码前缀_化成))
                _DataType = (int)DataType_Enum.化成;
            else if (TrayCode.StartsWith(CommonTypes.托盘码前缀_分容))
                _DataType = (int)DataType_Enum.分容;

            if (_DataType == 0)
                throw new Exception("数据类型错误");

            var payload = new { Position = Position, DataType = _DataType, InOutType = (int)InOutType, TrayCode = TrayCode };
            string model = JsonSerializer.Serialize(payload, JsonOptions);

            _logger.LogInformation("[杭可客户端] 托盘 {TrayCode} 出入库通知，传递数据：{Data}", TrayCode, model);

            // 构造 SOAP XML 信封
            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Header>
    <PackSoapHeader xmlns=""http://tempuri.org/"">
      <UserName>{_options.UserName}</UserName>
      <PassWord>{_options.PassWord}</PassWord>
    </PackSoapHeader>
  </soap:Header>
  <soap:Body>
    <TrayInOutBox xmlns=""http://tempuri.org/"">
      <json>{System.Security.SecurityElement.Escape(model)}</json>
    </TrayInOutBox>
  </soap:Body>
</soap:Envelope>";

            var url = $"{_options.Endpoint.TrimEnd('/')}/TrayInOutBox";
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "\"http://tempuri.org/TrayInOutBox\"");

            using var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[杭可客户端] 托盘 {TrayCode} 出入库通知，返回数据：{Data}", TrayCode, responseBody);

            // 解析 SOAP 响应
            var doc = XmlSafety.ParseSafe(responseBody);
            var ns = "http://tempuri.org/";
            var resultEl = doc.Descendants(XName.Get("TrayInOutBoxResult", ns)).FirstOrDefault();
            if (resultEl != null)
            {
                resultInfo = JsonSerializer.Deserialize<ResultInfo>(resultEl.Value, JsonOptions)
                            ?? new ResultInfo { ResultCode = -1, ResultMessage = "响应解析失败" };
            }
            else
            {
                resultInfo.ResultCode = -1;
                resultInfo.ResultMessage = "SOAP 响应中未找到 TrayInOutBoxResult";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[杭可客户端] 托盘 {TrayCode} 出入库通知异常", TrayCode);
            resultInfo.ResultMessage = "杭可出入库接口系统错误:" + ex.Message;
        }

        return resultInfo;
    }

    /// <summary>
    /// 获取电芯数据 — 通过 SOAP XML 调用杭可接口
    /// </summary>
    /// <param name="CellSn"></param>
    /// <returns></returns>
    public async Task<ResultInfo> GetCellDataAsync(string CellSn)
    {
        ResultInfo resultInfo = new ResultInfo();

        try
        {
            if (string.IsNullOrWhiteSpace(CellSn))
                throw new Exception("电芯条码不能空");

            var payload = new { CellSn = CellSn };
            string model = JsonSerializer.Serialize(payload, JsonOptions);

            _logger.LogInformation("[杭可客户端] 电芯 {CellSn} 检测数据查询，传递数据：{Data}", CellSn, model);

            // 构造 SOAP XML 信封
            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Header>
    <PackSoapHeader xmlns=""http://tempuri.org/"">
      <UserName>{_options.UserName}</UserName>
      <PassWord>{_options.PassWord}</PassWord>
    </PackSoapHeader>
  </soap:Header>
  <soap:Body>
    <GetCellDataBySnPK xmlns=""http://tempuri.org/"">
      <json>{System.Security.SecurityElement.Escape(model)}</json>
    </GetCellDataBySnPK>
  </soap:Body>
</soap:Envelope>";

            var url = $"{_options.Endpoint.TrimEnd('/')}/GetCellDataBySnPK";
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "\"http://tempuri.org/GetCellDataBySnPK\"");

            using var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[杭可客户端] 电芯 {CellSn} 检测数据查询，返回数据：{Data}", CellSn, responseBody);

            // 解析 SOAP 响应
            var doc = XmlSafety.ParseSafe(responseBody);
            var ns = "http://tempuri.org/";
            var resultEl = doc.Descendants(XName.Get("GetCellDataBySnPKResult", ns)).FirstOrDefault();
            if (resultEl != null)
            {
                resultInfo = JsonSerializer.Deserialize<ResultInfo>(resultEl.Value, JsonOptions)
                            ?? new ResultInfo { ResultCode = -1, ResultMessage = "响应解析失败" };
            }
            else
            {
                resultInfo.ResultCode = -1;
                resultInfo.ResultMessage = "SOAP 响应中未找到 GetCellDataBySnPKResult";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[杭可客户端] 电芯 {CellSn} 检测数据查询异常", CellSn);
            resultInfo.ResultMessage = "杭可接口系统错误:" + ex.Message;
        }

        return resultInfo;
    }
}
