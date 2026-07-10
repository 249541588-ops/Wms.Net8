using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Utilities.Response;

namespace Wms.Core.Infrastructure.Clients;

/// <summary>
/// WCS 通信客户端默认实现（基于 HTTP + Polly）
/// </summary>
public class DefaultWcsClient : IWcsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DefaultWcsClient> _logger;
    private readonly WcsClientOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DefaultWcsClient(
        HttpClient httpClient,
        ILogger<DefaultWcsClient> logger,
        IOptions<WcsClientOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 下发搬运任务到 WCS
    /// </summary>
    public async Task<WcsResult> SendTaskAsync(TransTask task)
    {
        try
        {
            var url = $"{_options.Endpoint.TrimEnd('/')}/api/wcs/task/send";
            var payload = new
            {
                taskCode = task.TaskCode,
                taskType = task.TaskType,
                unitloadId = task.UnitloadId,
                startLocationId = task.StartLocationId,
                endLocationId = task.EndLocationId
            };

            _logger.LogInformation("[WCS客户端] 下发搬运任务 taskCode={TaskCode}, endpoint={Endpoint}",
                task.TaskCode, url);

            return await PostAsync(url, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WCS客户端] 下发搬运任务失败 taskCode={TaskCode}", task.TaskCode);
            return ApiResultHelper.WcsFail(ex.Message, "SEND_TASK_ERROR", 0);
        }
    }

    /// <summary>
    /// 查询设备状态
    /// </summary>
    public async Task<WcsResult> GetEquipmentStatusAsync(string equipmentId)
    {
        try
        {
            var url = $"{_options.Endpoint.TrimEnd('/')}/api/wcs/equipment/{Uri.EscapeDataString(equipmentId)}/status";

            _logger.LogDebug("[WCS客户端] 查询设备状态 equipmentId={EquipmentId}", equipmentId);

            return await GetAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WCS客户端] 查询设备状态失败 equipmentId={EquipmentId}", equipmentId);
            return ApiResultHelper.WcsFail(ex.Message, "QUERY_STATUS_ERROR", 0);
        }
    }

    /// <summary>
    /// 上传 MES 信息到 WCS
    /// </summary>
    public async Task<WcsResult> UploadMesInfoAsync(UploadMesInfo info)
    {
        try
        {
            var url = $"{_options.Endpoint.TrimEnd('/')}/api/wcs/mes/upload";
            var payload = new
            {
                id = info.Id,
                containerCode = info.ContainerCode,
                locationCode = info.LocationCode,
                bizType = info.BizType,
                direction = info.Direction,
                opType = info.OpType,
                currentOperation = info.CurrentOperation,
                mestextInfo = info.MestextInfo
            };

            _logger.LogInformation("[WCS客户端] 上传MES信息 id={Id}, containerCode={ContainerCode}",
                info.Id, info.ContainerCode);

            return await PostAsync(url, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WCS客户端] 上传MES信息失败 id={Id}", info.Id);
            return ApiResultHelper.WcsFail(ex.Message, "UPLOAD_MES_ERROR", 0);
        }
    }

    private async Task<WcsResult> PostAsync(string url, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(url, content);
        return await ParseResponseAsync(response);
    }

    private async Task<WcsResult> GetAsync(string url)
    {
        using var response = await _httpClient.GetAsync(url);
        return await ParseResponseAsync(response);
    }

    private async Task<WcsResult> ParseResponseAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("[WCS客户端] 请求成功 statusCode={StatusCode}", (int)response.StatusCode);

            try
            {
                var result = JsonSerializer.Deserialize<WcsResult>(body, JsonOptions);
                return result ?? ApiResultHelper.WcsSuccess("操作成功", "OK", 0);
            }
            catch (JsonException)
            {
                return ApiResultHelper.WcsSuccess(body, "OK", 0);
            }
        }

        _logger.LogWarning("[WCS客户端] 请求失败 statusCode={StatusCode}, body={Body}",
            (int)response.StatusCode, body);

        return ApiResultHelper.WcsFail(
            $"WCS 返回错误: {(int)response.StatusCode} {response.ReasonPhrase}",
            $"HTTP_{(int)response.StatusCode}", 0);
    }
}
