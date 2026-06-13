using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 系统操作日志
/// </summary>
[Table("SystemLogs")]
public class SystemLog : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 操作时间
    /// </summary>
    public virtual DateTime OperationTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// HTTP 方法 (GET/POST/PUT/DELETE)
    /// </summary>
    [MaxLength(10)]
    public virtual string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// 控制器名称
    /// </summary>
    [MaxLength(100)]
    public virtual string? Module { get; set; }

    /// <summary>
    /// Action 名称
    /// </summary>
    [MaxLength(100)]
    public virtual string? Action { get; set; }

    /// <summary>
    /// 请求 URL 路径
    /// </summary>
    [MaxLength(500)]
    public virtual string? Url { get; set; }

    /// <summary>
    /// 响应状态码
    /// </summary>
    public virtual int? StatusCode { get; set; }

    /// <summary>
    /// 执行耗时(ms)
    /// </summary>
    public virtual long DurationMs { get; set; }

    /// <summary>
    /// 请求参数 (脱敏后)
    /// </summary>
    public virtual string RequestBody { get; set; } = string.Empty;

    /// <summary>
    /// IP 地址
    /// </summary>
    [MaxLength(50)]
    public virtual string? IpAddress { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    [MaxLength(100)]
    public virtual string? UserName { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    [MaxLength(100)]
    public virtual string? UserId { get; set; }

    /// <summary>
    /// UserAgent
    /// </summary>
    [MaxLength(500)]
    public virtual string? UserAgent { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public virtual bool Success { get; set; } = true;

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
