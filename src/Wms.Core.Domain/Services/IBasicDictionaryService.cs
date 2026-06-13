using Wms.Core.Domain.Entities;
using Wms.Core.Domain.ValueObjects;
using Wms.Core.Domain.Requests;
using Wms.Core.Domain.Common;

namespace Wms.Core.Domain.Services;

/// <summary>
/// 基础数据接口
/// </summary>
public interface IBasicDictionaryService
{
    /// <summary>
    /// 通过编码获取对象
    /// </summary>
    BasicDictionary GetByNo(string No);

    /// <summary>
    /// 通过编码获取明细对象
    /// </summary>
    List<BasicDictionary> GetItemsByNo(string No);

    /// <summary>
    /// 清除指定编码的缓存
    /// </summary>
    void ClearCache(string no);
}
