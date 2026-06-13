using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Utilities.Response;
using WcsRequestDto = Wms.Core.Application.DTOs.WcsRequest;

namespace Wms.Core.Application.Handlers.WcsRequest;

/// <summary>
/// WCS 请求处理器接口（策略模式 — 请求阶段）
/// </summary>
public interface IWcsRequestHandler
{
    /// <summary>
    /// 处理的请求类型（对应 Location.RequestType）
    /// </summary>
    string RequestType { get; }

    /// <summary>
    /// 处理 WCS 请求，返回结果
    /// </summary>
    Task<WcsResult> HandleAsync(WcsRequestDto request, Location location);
}
