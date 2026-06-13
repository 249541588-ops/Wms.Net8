using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Warehouse;

/// <summary>
/// 仓库 - 表示系统中的仓库信息
/// </summary>
[Table("Warehouses")]
public class Warehouse : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int Id { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public virtual int xVersion { get; set; }

    /// <summary>
    /// 用户编码
    /// </summary>
    [MaxLength(25)]
    public virtual string? UserCode { get; set; }

    /// <summary>
    /// 仓库名称
    /// </summary>
    [MaxLength(50)]
    public virtual string? xName { get; set; }

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
    [MaxLength(20)]
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(20)]
    public virtual string? ModifiedBy { get; set; }

    #endregion

    /// <summary>
    /// 电话
    /// </summary>
    [MaxLength(30)]
    public virtual string? Telephone { get; set; }

    /// <summary>
    /// 区域编码
    /// </summary>
    [MaxLength(10)]
    public virtual string? AreaCode { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    [MaxLength(50)]
    public virtual string? Address { get; set; }

    /// <summary>
    /// 邮编
    /// </summary>
    [MaxLength(10)]
    public virtual string? PostCode { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comments { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion

    /// <summary>
    /// 初始化仓库类的新实例
    /// </summary>
    public Warehouse()
    {
        CreatedTime = DateTime.Now;
        ModifiedTime = DateTime.Now;
    }
}
