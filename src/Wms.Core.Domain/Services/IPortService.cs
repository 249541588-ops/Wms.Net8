using Wms.Core.Domain.Entities;
using Wms.Core.Domain.ValueObjects;
using Wms.Core.Domain.Requests;
using Wms.Core.Domain.Common;

namespace Wms.Core.Domain.Services;

/// <summary>
/// 出库口接口
/// </summary>
public interface IPortService
{
    /// <summary>
    /// 通过编码获取明细
    /// </summary>
    Result CreatePort(CreatePortRequest No);

  
}
