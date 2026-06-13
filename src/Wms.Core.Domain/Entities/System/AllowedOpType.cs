using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 允许的操作类型 - 复合主键 (role_key + id)
/// </summary>
[Table("AllowedOpTypes")]
public class AllowedOpType : IEntity
{
    /// <summary>
    /// 角色键
    /// </summary>
    public virtual int role_key { get; set; }

    /// <summary>
    /// 操作类型ID
    /// </summary>
    [MaxLength(255)]
    public virtual string? id { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回第一个主键字段 role_key
    /// </summary>
    object IEntity.Id => role_key;

    #endregion
}
