using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using global::System.Text.Json.Serialization;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Warehouse;

/// <summary>
/// 端口 - 表示仓库中的端口信息
/// </summary>
[Table("Ports")]
public class Port : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 端口编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? PortCode { get; set; }

    /// <summary>
    /// 端口名称
    /// </summary>
    [MaxLength(255)]
    public virtual string? PortName { get; set; }

    /// <summary>
    /// 端口类型
    /// </summary>
    [MaxLength(255)]
    public virtual string? PortType { get; set; }

    /// <summary>
    /// 是否可用
    /// </summary>
    public virtual bool? IsAvailable { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// KP1
    /// </summary>
    //[JsonPropertyName("kp1")]
    public virtual int? KP1 { get; set; }

    /// <summary>
    /// KP2
    /// </summary>
    //[JsonPropertyName("kp2")]
    public virtual int? KP2 { get; set; }

    /// <summary>
    /// 当前UAT类型
    /// </summary>
    [MaxLength(30)]
    public virtual string? CurrentUatType { get; set; }

    /// <summary>
    /// 当前UAT ID
    /// </summary>
    public virtual int? CurrentUatId { get; set; }

    /// <summary>
    /// 检查时间
    /// </summary>
    public virtual DateTime CheckedAt { get; set; }

    /// <summary>
    /// 检查消息
    /// </summary>
    [MaxLength(255)]
    public virtual string? CheckMessage { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    [MaxLength(20)]
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(20)]
    public virtual string? ModifiedBy { get; set; }

    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion
}
