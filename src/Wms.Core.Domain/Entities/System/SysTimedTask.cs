using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 系统定时任务 - 复合主键 (TaskName + GroupName)
/// </summary>
[Table("TSysTimedTask")]
public class SysTimedTask : IEntity
{
    /// <summary>
    /// 进程ID
    /// </summary>
    public virtual long PID { get; set; }

    /// <summary>
    /// 任务名称
    /// </summary>
    public virtual string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// 组名称
    /// </summary>
    public virtual string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// 间隔
    /// </summary>
    [MaxLength(150)]
    public virtual string? Interval { get; set; }

    /// <summary>
    /// 接口地址
    /// </summary>
    [MaxLength(300)]
    public virtual string? ApiUrl { get; set; }

    /// <summary>
    /// 认证键
    /// </summary>
    [MaxLength(100)]
    public virtual string? AuthKey { get; set; }

    /// <summary>
    /// 认证值
    /// </summary>
    [MaxLength(100)]
    public virtual string? AuthValue { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [MaxLength(200)]
    public virtual string? Describe { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public virtual short Status { get; set; }

    /// <summary>
    /// 创建日期
    /// </summary>
    public virtual DateTime CreateDate { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    [MaxLength(50)]
    public virtual string? Creator { get; set; }

    /// <summary>
    /// 修改日期
    /// </summary>
    public virtual DateTime? ModifyDate { get; set; }

    /// <summary>
    /// 修改人
    /// </summary>
    [MaxLength(50)]
    public virtual string? Modifier { get; set; }

    /// <summary>
    /// 请求类型
    /// </summary>
    [MaxLength(50)]
    public virtual string? RequestType { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回第一个主键字段 TaskName
    /// </summary>
    object IEntity.Id => TaskName;

    #endregion
}
