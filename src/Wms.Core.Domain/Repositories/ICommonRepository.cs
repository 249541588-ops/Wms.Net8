using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;

namespace Wms.Core.Domain.Repositories;

/// <summary>
/// 公共仓储接口
/// </summary>
public interface ICommonRepository
{
    /// <summary>
    /// 获取电芯批次
    /// </summary>
    /// <param name="barcode">电芯条码</param>
    /// <returns></returns>
    string GetBattertBatch(string barcode);
}
