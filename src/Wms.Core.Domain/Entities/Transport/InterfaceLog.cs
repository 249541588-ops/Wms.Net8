using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Transport;

/// <summary>
/// 接口通信日志（WCS/MES 等外部系统接口）
/// </summary>
[Table("InterfaceLogs")]
public class InterfaceLog : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime CreatedTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 请求来源：WCS / MES / 内部定时任务 等
    /// </summary>
    [MaxLength(50)]
    public virtual string Source { get; set; } = string.Empty;

    /// <summary>
    /// 接口/方法名称
    /// </summary>
    [MaxLength(100)]
    public virtual string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 请求者/设备标识
    /// </summary>
    [MaxLength(100)]
    public virtual string? Requester { get; set; }

    /// <summary>
    /// 请求位置
    /// </summary>
    [MaxLength(50)]
    public virtual string? LocationCode { get; set; }

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(100)]
    public virtual string? ContainerCode { get; set; }

    /// <summary>
    /// 请求数据体 (截断 8000)
    /// </summary>
    public virtual string RequestBody { get; set; } = string.Empty;

    /// <summary>
    /// 返回处理结果 (截断 8000)
    /// </summary>
    public virtual string ResponseBody { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public virtual bool Success { get; set; }

    /// <summary>
    /// 处理用时(ms)
    /// </summary>
    public virtual long DurationMs { get; set; }

    /// <summary>
    /// 是否重复请求（短时间相同请求）
    /// </summary>
    public virtual bool IsDuplicate { get; set; }

    /// <summary>
    /// 备注/错误信息
    /// </summary>
    [MaxLength(2000)]
    public virtual string? Comment { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion
}
