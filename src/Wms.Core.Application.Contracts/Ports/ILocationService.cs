using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Requests;
using Wms.Core.Domain.ValueObjects;

namespace Wms.Core.Application.Ports;

/// <summary>
/// 货位数据接口
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// 获取货位
    /// </summary>
    /// <param name="locationCode"></param>
    /// <returns></returns>
    Location GetLocation(string locationCode);


}
