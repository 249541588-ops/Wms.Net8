using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Repositories;
using Wms.Core.Application.Ports;
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
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    // 账号级登录失败计数器（内存级，服务重启后清零）
    private static readonly ConcurrentDictionary<string, (int Count, DateTime LockedUntil)> _loginAttempts = new();
    private const int MaxFailedAttempts = 10;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 初始化认证服务类的新实例
    /// </summary>
    public AuthService(
        IUserRepository userRepository,
        IRepository<User, int> repository,
        WmsDbContext db,
        IPasswordHasher passwordHasher,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    public async Task<User?> LoginAsync(string username, string password)
    {
        try
        {
            // 账号级暴力破解检查
            if (_loginAttempts.TryGetValue(username, out var attempt) && attempt.LockedUntil > DateTime.UtcNow)
            {
                _logger.LogWarning("登录失败: 用户 {Username} 已被临时锁定（连续失败 {Count} 次）", username, attempt.Count);
                return null;
            }

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

            if (!string.IsNullOrWhiteSpace(password) && !VerifyUserPassword(user, password))
            {
                // 记录失败次数
                _loginAttempts.AddOrUpdate(username,
                    _ => (1, DateTime.MinValue),
                    (_, prev) => (prev.Count + 1, prev.Count + 1 >= MaxFailedAttempts ? DateTime.UtcNow.Add(LockoutDuration) : prev.LockedUntil));
                _logger.LogWarning("登录失败: 用户 {Username} 密码错误（累计 {Count} 次）", username, _loginAttempts[username].Count);
                return null;
            }

            // 更新最后登录时间 + 清除失败计数
            user.RecordLogin();
            _loginAttempts.TryRemove(username, out _);

            // 自动升级旧 HMAC 密码为 BCrypt（rehash on verify）
            if (user.NeedsPasswordRehash())
            {
                user.SetPassword(password);
            }

            _db.Update(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("用户 {Username} 登录成功", username);

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录失败: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 创建新用户
    /// </summary>
    public async Task<User> CreateUserAsync(string username, string password, string RealName, string Email, string PhoneNumber, string role = "User", string? createdBy = null)
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
        var roleEntity = await _db.Set<Role>().FirstOrDefaultAsync(r => r.RoleName == role);
        if (roleEntity != null)
        {
            await _db.Database.ExecuteSqlRawAsync(
                "INSERT INTO UserRoles (UserId, RoleId) VALUES (@userId, @roleId)",
                new Microsoft.Data.SqlClient.SqlParameter("@userId", savedUser.Id),
                new Microsoft.Data.SqlClient.SqlParameter("@roleId", roleEntity.Id));
        }

        _logger.LogInformation("创建用户成功: {Username}, 角色: {Role}", username, role);

        return savedUser;
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
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
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
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
    }

    /// <summary>
    /// 验证用户密码（User.ValidatePassword 已兼容 BCrypt 和旧 HMAC 格式）
    /// </summary>
    private bool VerifyUserPassword(User user, string password)
    {
        return user.ValidatePassword(password);
    }
}
