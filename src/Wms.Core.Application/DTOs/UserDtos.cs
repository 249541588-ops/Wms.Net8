using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wms.Core.Domain.Entities.Identity;

namespace Wms.Core.Application.DTOs;

/// <summary>
/// 创建用户请求
/// </summary>
public record CreateUserRequest
{
    /// <summary>
    /// 用户名
    /// </summary>
    [Required(ErrorMessage = "用户名不能为空")]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    [Required(ErrorMessage = "密码不能为空")]
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    public string RealName { get; init; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// 
    /// </summary>
    public string? PhoneNumber { get; init; }

    /// <summary>
    /// 角色
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// 创建用户
    /// </summary>
    public string? CreatedBy { get; init; }
}

/// <summary>
/// 
/// </summary>
public record UpdateUserRequest
{   
    /// <summary>
    /// 
    /// </summary>
    public string RealName { get; init; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    public string? Email { get; init; }
    
    /// <summary>
    /// 
    /// </summary>
     public string? PhoneNumber { get; init; }

    /// <summary>
    /// 用户
    /// </summary>
    public string? ModifiedBy { get; init; }

    /// <summary>
    /// 角色
    /// </summary>
    public string? Role { get; init; }
}

/// <summary>
/// 设置用户启用状态请求
/// </summary>
public record SetUserEnabledRequest
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public int Type { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 修改用户
    /// </summary>
    public string? ModifiedBy { get; init; }
}

/// <summary>
/// 用户管理 - 用户响应（排除 PasswordHash、PasswordSalt 等敏感字段）
/// </summary>
public record UserResponse
{
    public int Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? RealName { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsBuiltIn { get; init; }
    public bool IsLocked { get; init; }
    public string? LockedReason { get; init; }
    public DateTime? LastLoginTime { get; init; }
    public string? LastLoginIp { get; init; }
    public bool IsActive { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
    public DateTime? CreatedTime { get; init; }
    public DateTime? ModifiedTime { get; init; }
    public string? CreatedBy { get; init; }
    public string? ModifiedBy { get; init; }

    /// <summary>
    /// 角色名称（与 User 实体 JSON 序列化保持一致，统一小写 role）
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>
    /// 从 User 实体映射到安全响应对象
    /// </summary>
    public static UserResponse From(User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        RealName = user.RealName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        IsBuiltIn = user.IsBuiltIn,
        IsLocked = user.IsLocked,
        LockedReason = user.LockedReason,
        LastLoginTime = user.LastLoginTime,
        LastLoginIp = user.LastLoginIp,
        IsActive = user.IsActive,
        DeletedAt = user.DeletedAt,
        DeletedBy = user.DeletedBy,
        CreatedTime = user.CreatedTime,
        ModifiedTime = user.ModifiedTime,
        CreatedBy = user.CreatedBy,
        ModifiedBy = user.ModifiedBy,
        Role = user.Role
    };
}

/// <summary>
/// 个人中心 - 当前用户资料响应
/// </summary>
public record ProfileResponse
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// 真实姓名
    /// </summary>
    public string? RealName { get; init; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// 手机号
    /// </summary>
    public string? PhoneNumber { get; init; }
}

/// <summary>
/// 个人中心 - 自助更新资料请求
/// </summary>
public record UpdateProfileRequest
{
    /// <summary>
    /// 真实姓名
    /// </summary>
    [MaxLength(64, ErrorMessage = "真实姓名最长 64 字符")]
    public string? RealName { get; init; }

    /// <summary>
    /// 邮箱
    /// </summary>
    [EmailAddress(ErrorMessage = "邮箱格式不合法")]
    [MaxLength(128, ErrorMessage = "邮箱最长 128 字符")]
    public string? Email { get; init; }

    /// <summary>
    /// 手机号
    /// </summary>
    [Phone(ErrorMessage = "手机号格式不合法")]
    [MaxLength(20, ErrorMessage = "手机号最长 20 字符")]
    public string? PhoneNumber { get; init; }
}
