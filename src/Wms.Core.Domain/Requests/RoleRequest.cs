namespace Wms.Core.Domain.Requests;

/// <summary>
/// 创建请求
/// </summary>
public record CreateRoleRequest
{
    /// <summary>
    /// 
    /// </summary>
    public string RoleName { get; init; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    public string? CreatedBy { get; init; }
}

/// <summary>
/// 
/// </summary>
public record UpdateRoleRequest
{   
    /// <summary>
    /// 
    /// </summary>
    public string RoleName { get; init; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// 
    /// </summary>
     public string? ModifiedBy { get; init; }   

    /// <summary>
    /// 
    /// </summary>
    public bool IsBuiltIn { get; set; }
}

/// <summary>
/// 设置权限请求
/// </summary>
public record SetPermissionRequest
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
/// 设置菜单请求
/// </summary>
public record SetMenuRequest
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
/// 设置角色菜单请求
/// </summary>
public record SettingRoleMenusRequest
{
    /// <summary>
    /// 
    /// </summary>
    public int MenuId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string[] Btn { get; set; }
}

