using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Warehouse;

/// <summary>
/// 通道端口关联 - 表示通道与端口的关系（复合主键）
/// </summary>
[Table("Laneway_Port")]
public class LanewayPort : IEntity
{
    /// <summary>
    /// 端口编号（主键之一）
    /// </summary>
    public virtual int PortId { get; set; }

    /// <summary>
    /// 通道编号（主键之一）
    /// </summary>
    public virtual int LanewayId { get; set; }

    /// <summary>
    /// 关联的端口
    /// </summary>
    public virtual Port? Port { get; set; }

    /// <summary>
    /// 关联的通道
    /// </summary>
    public virtual Laneway? Laneway { get; set; }

    #region IEntity 成员

    object IEntity.Id => LanewayId;

    #endregion
}
