using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 后台作业
/// </summary>
[Table("BackgroundJobs")]
public class BackgroundJob : IEntity<Guid>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual Guid Id { get; set; }

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
    [MaxLength(255)]
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(255)]
    public virtual string? ModifiedBy { get; set; }

    #endregion

    /// <summary>
    /// 作业类型
    /// </summary>
    [MaxLength(255)]
    public virtual string? JobType { get; set; }

    /// <summary>
    /// API 地址（internal 模式为方法标识，http-call 模式为 API 相对路径）
    /// </summary>
    [MaxLength(500)]
    public virtual string? ApiUrl { get; set; }

    /// <summary>
    /// 请求方式（仅 http-call 模式：GET/POST/PUT/DELETE）
    /// </summary>
    [MaxLength(10)]
    public virtual string? RequestType { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    [MaxLength(255)]
    public virtual string? Name { get; set; }

    /// <summary>
    /// 作业名称
    /// </summary>
    [MaxLength(255)]
    public virtual string? JobName { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [MaxLength(255)]
    public virtual string? Description { get; set; }       

    /// <summary>
    /// 负载
    /// </summary>
    [MaxLength(255)]
    public virtual string? Payload { get; set; }           


    /// <summary>
    /// 作业参数
    /// </summary>
    [MaxLength(255)]
    public virtual string? JobArgs { get; set; }

    /// <summary>
    /// Cron表达式
    /// </summary>
    [MaxLength(255)]
    public virtual string? CronExpression { get; set; }

    /// <summary>
    /// Cron表达式描述
    /// </summary>
    [MaxLength(255)]
    public virtual string? CronExpressionDescription { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public virtual int? State { get; set; }

    /// <summary>
    /// 显示排序
    /// </summary>
    public virtual int? DisplayOrder { get; set; }

    /// <summary>
    /// 是否删除
    /// </summary>
    public virtual bool? IsDeleted { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
