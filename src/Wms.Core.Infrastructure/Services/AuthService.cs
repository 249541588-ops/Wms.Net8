using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Services;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRepository<User, int> _repository;
    private readonly WmsDbContext _db;
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// 初始化认证服务类的新实例
    /// </summary>
    public AuthService(
        IUserRepository userRepository,
        IRepository<User, int> repository,
        WmsDbContext db,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    public async Task<User?> LoginAsync(string username, string password)
    {
        return await Task.Run(() =>
        {
            try
            {
                var user = _userRepository.GetByUsername(username);
                if (user == null)
                {
                    _logger.LogWarning("登录失败: 用户名 {Username} 不存在", username);
                    return null;
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("登录失败: 用户 {Username} 已被禁用", username);
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(password) && !user.ValidatePassword(password))
                {
                    _logger.LogWarning("登录失败: 用户 {Username} 密码错误", username);
                    return null;
                }

                // 更新最后登录时间
                user.RecordLogin();
                _db.Update(user);
                _db.SaveChangesAsync().Wait();

                _logger.LogInformation("用户 {Username} 登录成功", username);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登录失败: {Message}", ex.Message);
                return null;
            }
        });
    }

    /// <summary>
    /// 创建新用户
    /// </summary>
    public async Task<User> CreateUserAsync(string username, string password, string RealName, string Email, string PhoneNumber, string role = "User", string? createdBy = null)
    {
        return await Task.Run(() =>
        {
            // 验证用户名唯一性
            if (_userRepository.Exists(username))
            {
                throw new InvalidOperationException($"用户名 {username} 已存在");
            }

            var user = new User
            {
                UserName = username,
                RealName = RealName,
                Email = Email,
                PhoneNumber = PhoneNumber,
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                ModifiedTime = DateTime.UtcNow,
                CreatedBy = createdBy
            };
            user.SetPassword(password);

            var savedUser = _repository.Add(user);

            // 查找角色并创建关联
            var roleEntity = _db.Set<Role>().FirstOrDefault(r => r.RoleName == role);
            if (roleEntity != null)
            {
                _db.Database.ExecuteSqlRaw(
                    "INSERT INTO UserRoles (UserId, RoleId) VALUES (@userId, @roleId)",
                    new Microsoft.Data.SqlClient.SqlParameter("@userId", savedUser.Id),
                    new Microsoft.Data.SqlClient.SqlParameter("@roleId", roleEntity.Id));
            }

            _logger.LogInformation("创建用户成功: {Username}, 角色: {Role}", username, role);

            return savedUser;
        });
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        return await Task.Run(() =>
        {
            try
            {
                var user = _repository.GetById(userId);
                if (user == null)
                {
                    _logger.LogWarning("修改密码失败: 用户 ID {UserId} 不存在", userId);
                    return false;
                }

                if (!user.ValidatePassword(oldPassword))
                {
                    _logger.LogWarning("修改密码失败: 用户 {Username} 旧密码错误", user.UserName);
                    return false;
                }

                user.SetPassword(newPassword);
                _repository.Update(user);

                _logger.LogInformation("用户 {Username} 修改密码成功", user.UserName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修改密码失败: {Message}", ex.Message);
                return false;
            }
        });
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
    {
        return await Task.Run(() =>
        {
            try
            {
                var user = _repository.GetById(userId);
                if (user == null)
                {
                    _logger.LogWarning("重置密码失败: 用户 ID {UserId} 不存在", userId);
                    return false;
                }                

                user.SetPassword(newPassword);
                _repository.Update(user);

                _logger.LogInformation("用户 {Username} 重置密码成功", user.UserName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重置密码失败: {Message}", ex.Message);
                return false;
            }
        });
    }
}
