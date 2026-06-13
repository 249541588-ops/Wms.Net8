using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using global::System.Text.Json.Serialization;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Identity;

/// <summary>
/// 角色 - 表示用户角色
/// </summary>
public class Role : IEntity<int>, IAuditable
{

    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    #region IAuditable 实现

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    [MaxLength(64)]
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(64)]
    public virtual string? ModifiedBy { get; set; }

    #endregion

    /// <summary>
    /// 角色名称（自然键）
    /// </summary>
    [Required]
    [MaxLength(64)]
    public virtual string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// 角色描述
    /// </summary>
    [MaxLength(255)]
    public virtual string? Description { get; set; }

    /// <summary>
    /// 是否内置角色
    /// </summary>
    public virtual bool IsBuiltIn { get; set; }

    /// <summary>
    /// 允许的操作类型集合（不持久化到数据库）
    /// 注意：此属性已废弃，权限通过 AuthSetting 实体管理
    /// </summary>
    [global::System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [Obsolete("权限通过 AuthSetting 实体管理")]
    public virtual IEnumerable<string> AllowedOpTypes => Enumerable.Empty<string>();

    /// <summary>
    /// 用户集合
    /// </summary>
    [JsonIgnore]
    public virtual ISet<User> Users { get; set; } = new HashSet<User>();
       
    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion

    /// <summary>
    /// 初始化角色类的新实例
    /// </summary>
    public Role()
    {
        CreatedTime = DateTime.Now;
        ModifiedTime = DateTime.Now;
    }
}

/// <summary>
/// 角色菜单
/// </summary>
[global::System.ComponentModel.DataAnnotations.Schema.Table("Role_Menu")]
public class Role_Menu
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual long Id { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public virtual int MenuId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public virtual int RoleId { get; set; }
}

/// <summary>
/// 角色菜单按钮
/// </summary>
[global::System.ComponentModel.DataAnnotations.Schema.Table("Role_Menu_Funs")]
public class Role_Menu_Funs : IEntity<long>
{

    /// <summary>
    /// 主键
    /// </summary>
    public virtual long Id { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    public virtual int MenuId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public virtual int RoleId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [MaxLength(64)]
    public virtual string? FunctionButton { get; set; }
       
    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion

    /// <summary>
    /// 初始化角色类的新实例
    /// </summary>
    public Role_Menu_Funs()
    {
        
    }
}
