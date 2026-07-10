using System.ComponentModel.DataAnnotations;

namespace Wms.Core.WebApi.Models;

/// <summary>
/// 登录请求
/// </summary>
public record LoginRequest
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
}

/// <summary>
/// 登录响应
/// </summary>
public record LoginResponse
{
    /// <summary>
    /// JWT Token
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// 刷新 Token
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>
    /// Token 过期时间（UTC）
    /// </summary>
    public DateTime Expiration { get; init; }

    /// <summary>
    /// 用户信息
    /// </summary>
    public UserInfo User { get; init; } = new();
}

/// <summary>
/// 用户信息
/// </summary>
public record UserInfo
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 角色
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// 权限列表
    /// </summary>
    public string[] Permissions { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 刷新 Token 请求
/// </summary>
public record RefreshTokenRequest
{
    /// <summary>
    /// 刷新 Token
    /// </summary>
    [Required(ErrorMessage = "刷新 Token 不能为空")]
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// 修改密码请求
/// </summary>
public record ChangePasswordRequest
{
    /// <summary>
    /// 旧密码
    /// </summary>
    [Required(ErrorMessage = "旧密码不能为空")]
    public string OldPassword { get; init; } = string.Empty;

    /// <summary>
    /// 新密码
    /// </summary>
    [Required(ErrorMessage = "新密码不能为空")]
    [MinLength(6, ErrorMessage = "新密码至少 6 位")]
    [MaxLength(64, ErrorMessage = "新密码最长 64 字符")]
    public string NewPassword { get; init; } = string.Empty;
}
