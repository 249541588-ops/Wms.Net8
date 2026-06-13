using Wms.Core.Domain.Entities.Identity;

namespace Wms.Core.Domain.Repositories;

/// <summary>
/// 用户仓储接口
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// 根据Id获取用户
    /// </summary>
    User? GetByUserid(int userid);

    /// <summary>
    /// 根据用户名获取用户
    /// </summary>
    User? GetByUsername(string username);

    /// <summary>
    /// 检查用户名是否存在
    /// </summary>
    bool Exists(string username);

    /// <summary>
    /// 获取所有用户
    /// </summary>
    IQueryable<User> GetAll();

    /// <summary>
    /// 根据角色获取用户列表
    /// </summary>
    IQueryable<User> GetByRole(string role);
}
