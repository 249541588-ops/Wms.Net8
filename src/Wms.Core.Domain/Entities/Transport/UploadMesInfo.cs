using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Transport;

/// <summary>
/// MES上传信息
/// </summary>
[Table("UploadMesInfo")]
public class UploadMesInfo : IEntity<int>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 容器编码
    /// </summary>
    [MaxLength(20)]
    public virtual string? ContainerCode { get; set; }

    /// <summary>
    /// 库位编码
    /// </summary>
    [MaxLength(20)]
    public virtual string? LocationCode { get; set; }

    /// <summary>
    /// 业务类型
    /// </summary>
    [MaxLength(20)]
    public virtual string? BizType { get; set; }

    /// <summary>
    /// 方向
    /// </summary>
    public virtual int? Direction { get; set; }

    /// <summary>
    /// 操作类型
    /// </summary>
    [MaxLength(20)]
    public virtual string? OpType { get; set; }

    /// <summary>
    /// 当前操作
    /// </summary>
    [MaxLength(20)]
    public virtual string? CurrentOperation { get; set; }

    /// <summary>
    /// MES文本信息（nvarchar(MAX)）
    /// </summary>
    public virtual string MestextInfo { get; set; } = string.Empty;

    /// <summary>
    /// MES标志
    /// </summary>
    public virtual int MesIsFlag { get; set; } = 0;

    /// <summary>
    /// MES消息
    /// </summary>
    [MaxLength(2000)]
    public virtual string? MesMsg { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime ctime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime? mtime { get; set; }

    /// <summary>
    /// 数量
    /// </summary>
    public virtual decimal? Quantity { get; set; }

    /// <summary>
    /// 错误计数
    /// </summary>
    public virtual int ErrCount { get; set; } = 0;

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
