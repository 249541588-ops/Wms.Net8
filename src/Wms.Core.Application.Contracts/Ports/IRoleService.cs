using Wms.Core.Domain.Entities;
using Wms.Core.Domain.ValueObjects;
using Wms.Core.Domain.Requests;

namespace Wms.Core.Application.Ports;

/// <summary>
/// 库存服务接口
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// 初始化 admin 角色按钮权限
    /// </summary>
    Task<bool> InitialConfiguration();

    /// <summary>
    /// 设置角色菜单按钮权限
    /// </summary>
    /// <param name="Roleid"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    Task<bool> SettingRoleMenus(int Roleid, List<SettingRoleMenusRequest> request);

}
