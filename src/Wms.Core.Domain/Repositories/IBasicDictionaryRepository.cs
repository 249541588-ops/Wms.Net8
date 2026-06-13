using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.ValueObjects;

namespace Wms.Core.Domain.Repositories;

/// <summary>
/// 基础数据仓储接口
/// </summary>
public interface IBasicDictionaryRepository
{
    /// <summary>
    /// 通过编码获取
    /// </summary>
    /// <param name="lx">1.api 2.系统</param>
    /// <param name="no">编码</param>
    /// <returns></returns>
    List<BasicDictionary> GetListByNo(int lx, string no);

    /// <summary>
    /// 通过编码获取父级节点下的子节点
    /// </summary>
    /// <param name="lx">1.api 2.系统</param>
    /// <param name="no">编码</param>
    /// <returns></returns>
    List<BasicDictionary> GetChildListByNo(int lx, string no);

    /// <summary>
    /// 获取数据字典数据
    /// </summary>
    /// <param name="lx">1.接口 2.系统</param>
    /// <param name="type">1.通过no获取 2.通过name获取</param>
    /// <param name="name">传入参数</param>
    /// <returns></returns>
    BasicDictionary GetBasicDictionary(int lx, int type, string name);
    

}
