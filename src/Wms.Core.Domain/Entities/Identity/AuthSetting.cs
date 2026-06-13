using global::System.ComponentModel.DataAnnotations;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Identity;

/// <summary>
/// 权限设置 - 定义操作类型与角色的关联关系
/// </summary>
public class AuthSetting : IEntity<string>
{
    /// <summary>
    /// 主键（操作类型）
    /// </summary>
    public virtual string Id { get; set; } = string.Empty;

    /// <summary>
    /// 操作类型（与 Id 相同）
    /// 例如：User.Create, Order.Approve, Task.Complete 等
    /// </summary>
    [Required]
    [MaxLength(100)]
    public virtual string OpType { get; set; } = string.Empty;

    /// <summary>
    /// 允许执行此操作的角色列表（JSON 数组格式）
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public virtual string AllowedRoles { get; set; } = "[]";

    /// <summary>
    /// 操作描述
    /// </summary>
    [MaxLength(255)]
    public virtual string? Description { get; set; }

    /// <summary>
    /// 模块
    /// </summary>
    [MaxLength(50)]
    public virtual string? Module { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public virtual bool Enabled { get; set; } = true;

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public virtual string? Comment { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion

    /// <summary>
    /// 初始化权限设置类的新实例
    /// </summary>
    public AuthSetting()
    {
        Enabled = true;
        AllowedRoles = "[]";
    }

    /// <summary>
    /// 获取允许的角色列表
    /// </summary>
    public virtual List<string> GetAllowedRoles()
    {
        if (string.IsNullOrEmpty(AllowedRoles))
        {
            return new List<string>();
        }

        try
        {
            return global::System.Text.Json.JsonSerializer.Deserialize<List<string>>(AllowedRoles, new global::System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// 设置允许的角色列表
    /// </summary>
    public virtual void SetAllowedRoles(List<string> roles)
    {
        if (roles == null)
        {
            AllowedRoles = "[]";
        }
        else
        {
            AllowedRoles = global::System.Text.Json.JsonSerializer.Serialize(roles);
        }
    }

    /// <summary>
    /// 检查指定角色是否有权限
    /// </summary>
    public virtual bool IsRoleAllowed(string roleName)
    {
        var allowedRoles = GetAllowedRoles();
        return allowedRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 添加允许的角色
    /// </summary>
    public virtual void AddAllowedRole(string roleName)
    {
        var allowedRoles = GetAllowedRoles();
        if (!allowedRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
        {
            allowedRoles.Add(roleName);
            SetAllowedRoles(allowedRoles);
        }
    }

    /// <summary>
    /// 移除允许的角色
    /// </summary>
    public virtual void RemoveAllowedRole(string roleName)
    {
        var allowedRoles = GetAllowedRoles();
        allowedRoles.RemoveAll(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));
        SetAllowedRoles(allowedRoles);
    }

    /// <summary>
    /// 启用权限设置
    /// </summary>
    public virtual void Enable()
    {
        Enabled = true;
    }

    /// <summary>
    /// 禁用权限设置
    /// </summary>
    public virtual void Disable()
    {
        Enabled = false;
    }
}
