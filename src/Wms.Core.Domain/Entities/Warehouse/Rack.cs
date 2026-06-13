using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Warehouse;

/// <summary>
/// 货架 - 表示仓库中的货架信息
/// </summary>
[Table("Racks")]
public class Rack : IEntity<int>, IAuditable
{
    /// <summary>
    /// 货架编号（主键）
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public virtual int RackId { get; set; }

    /// <summary>
    /// 仓库编号
    /// </summary>
    public virtual int? WarehouseId { get; set; }

    /// <summary>
    /// 通道编号
    /// </summary>
    public virtual int? LanewayId { get; set; }

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
    /// 货架编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? RackCode { get; set; }

    /// <summary>
    /// 侧面
    /// </summary>
    public virtual int? Side { get; set; }

    /// <summary>
    /// 深度
    /// </summary>
    public virtual int? Deep { get; set; }

    /// <summary>
    /// 列数
    /// </summary>
    public virtual int? Columns { get; set; }

    /// <summary>
    /// 层数
    /// </summary>
    public virtual int? Levels { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 仓库
    /// </summary>
    public virtual Warehouse? Warehouse { get; set; }

    /// <summary>
    /// 通道
    /// </summary>
    public virtual Laneway? Laneway { get; set; }

    /// <summary>
    /// 初始化货架类的新实例
    /// </summary>
    public Rack()
    {
        CreatedTime = DateTime.Now;
        ModifiedTime = DateTime.Now;
    }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 RackId
    /// </summary>
    int IEntity<int>.Id { get => RackId; set => RackId = value; }

    /// <summary>
    /// 显式接口实现 - 返回 RackId
    /// </summary>
    object IEntity.Id => RackId;

    #endregion
}
