using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 角色操作类型 - 复合主键 (RoleId + OpType)
/// </summary>
[Table("ROLE_OPTYPE")]
public class RoleOpType : IEntity
{
    /// <summary>
    /// 角色ID
    /// </summary>
    public virtual int RoleId { get; set; }

    /// <summary>
    /// 操作类型
    /// </summary>
    [MaxLength(255)]
    public virtual string? OpType { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回第一个主键字段 RoleId
    /// </summary>
    object IEntity.Id => RoleId;

    #endregion
}
