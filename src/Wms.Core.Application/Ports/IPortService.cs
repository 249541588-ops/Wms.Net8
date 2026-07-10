using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Requests;
using Wms.Core.Domain.ValueObjects;

namespace Wms.Core.Application.Ports;

/// <summary>
/// 出库口接口
/// </summary>
public interface IPortService
{
    /// <summary>
    /// 通过编码获取明细
    /// </summary>
    Task<Result> CreatePort(CreatePortRequest No);


}
