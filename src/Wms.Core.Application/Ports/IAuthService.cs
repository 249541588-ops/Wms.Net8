using Wms.Core.Domain.Entities.Identity;

namespace Wms.Core.Application.Ports;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>用户实体（认证成功返回用户，失败返回 null）</returns>
    Task<User?> LoginAsync(string username, string password);

    /// <summary>
    /// 创建新用户
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="RealName">显示名称</param>
    /// <param name="role">角色</param>
    /// <param name="createdBy">创建人</param>
    /// <returns>创建的用户</returns>
    Task<User> CreateUserAsync(string username, string password, string RealName,string Email,string PhoneNumber, string role = "User", string? createdBy = null);

    /// <summary>
    /// 修改密码
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="oldPassword">旧密码</param>
    /// <param name="newPassword">新密码</param>
    /// <returns>是否成功</returns>
    Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);

    /// <summary>
    /// 重置密码
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="newPassword">新密码</param>
    /// <returns>是否成功</returns>
    Task<bool> ResetPasswordAsync(int userId, string newPassword);

}
