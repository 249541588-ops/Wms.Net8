namespace Wms.Core.Application.DTOs;

/// <summary>
/// 创建用户请求
/// </summary>
public record CreateUserRequest
{
    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
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
